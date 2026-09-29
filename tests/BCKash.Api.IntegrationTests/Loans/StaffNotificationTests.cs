using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>Repayments waiting for the office manager, and the office portal's bell notifications.</summary>
public class StaffNotificationTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private static readonly string[] AdminPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    private readonly BCKashWebApplicationFactory _factory;

    public StaffNotificationTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_repayment_waits_for_the_office_manager_and_only_counts_once_approved()
    {
        var office = await SetUpOfficeAsync("approve");
        var loanId = await CreateDisbursedLoanAsync(office.OfficeId, "ApproveRepay");

        var recorded = await office.Marketer.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), "Paid at branch"));
        Assert.Equal(HttpStatusCode.Accepted, recorded.StatusCode);
        var submission = await recorded.Content.ReadFromJsonAsync<RepaymentSubmissionResponse>(TestJson.Options);
        Assert.False(submission!.CanReview);

        // Not counted yet.
        Assert.Equal(0, await RepaymentCountAsync(office.Manager, loanId));

        // The manager's bell has it; the marketer who recorded it isn't told about their own action.
        Assert.Equal(1, await OpenCountAsync(office.Manager));
        var pending = Assert.Single(await ListAsync(office.Manager), n => n.Kind == UserNotificationKinds.RepaymentPending);
        Assert.True(pending.NeedsAction);
        Assert.Equal($"/loans/{loanId}?section=repayments", pending.Link);
        Assert.Equal(0, await OpenCountAsync(office.Marketer));

        // Only the manager confirms.
        Assert.Equal(HttpStatusCode.Forbidden, (await office.Marketer.PostAsync($"/api/v1/loans/{loanId}/repayments/submissions/{submission.Id}/approve", null)).StatusCode);

        // Opening an action notification doesn't clear it — dealing with the item does.
        await office.Manager.PostAsync($"/api/v1/notifications/{pending.Id}/read", null);
        Assert.Equal(1, await OpenCountAsync(office.Manager));

        var listed = Assert.Single(await office.Manager.GetFromJsonAsync<List<RepaymentSubmissionResponse>>($"/api/v1/loans/{loanId}/repayments/submissions", TestJson.Options) ?? []);
        Assert.True(listed.CanReview);

        var approved = await office.Manager.PostAsync($"/api/v1/loans/{loanId}/repayments/submissions/{submission.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var result = await approved.Content.ReadFromJsonAsync<RepaymentSubmissionResponse>(TestJson.Options);
        Assert.Equal(RepaymentSubmissionStatus.Approved, result!.Status);
        Assert.NotNull(result.LoanTransactionId);
        Assert.Equal(1, await RepaymentCountAsync(office.Manager, loanId));

        Assert.Equal(0, await OpenCountAsync(office.Manager));
        Assert.Equal(HttpStatusCode.Conflict, (await office.Manager.PostAsync($"/api/v1/loans/{loanId}/repayments/submissions/{submission.Id}/approve", null)).StatusCode);

        // The marketer hears it was confirmed; reading news closes it.
        var news = Assert.Single(await ListAsync(office.Marketer), n => n.Kind == UserNotificationKinds.RepaymentReviewed);
        Assert.Contains("confirmed", news.Title);
        await office.Marketer.PostAsync($"/api/v1/notifications/{news.Id}/read", null);
        Assert.Equal(0, await OpenCountAsync(office.Marketer));
    }

    [Fact]
    public async Task Pending_repayments_count_against_what_can_still_be_recorded()
    {
        var office = await SetUpOfficeAsync("limit");
        var loanId = await CreateDisbursedLoanAsync(office.OfficeId, "LimitRepay");
        // A savings loan: 13,440 owed, which the client pays grossed up — 13,440 ÷ 0.975 = 13,784.62.
        Assert.Equal(HttpStatusCode.Accepted, (await office.Marketer.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(13000m, null, null, null))).StatusCode);

        var tooMuch = await office.Marketer.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1000m, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("at most ₦784.62 more", await tooMuch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted, (await office.Marketer.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(784.62m, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task A_disputed_repayment_never_counts()
    {
        var office = await SetUpOfficeAsync("dispute");
        var loanId = await CreateDisbursedLoanAsync(office.OfficeId, "DisputeRepay");
        var submission = await (await office.Marketer.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(5000m, null, null, null)))
            .Content.ReadFromJsonAsync<RepaymentSubmissionResponse>(TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, (await office.Manager.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments/submissions/{submission!.Id}/dispute", new ReasonRequest(" "))).StatusCode);

        var disputed = await office.Manager.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments/submissions/{submission.Id}/dispute", new ReasonRequest("No such deposit on the statement"));
        Assert.Equal(HttpStatusCode.OK, disputed.StatusCode);
        Assert.Equal(RepaymentSubmissionStatus.Disputed, (await disputed.Content.ReadFromJsonAsync<RepaymentSubmissionResponse>(TestJson.Options))!.Status);

        Assert.Equal(0, await RepaymentCountAsync(office.Manager, loanId));
        Assert.Equal(0, await OpenCountAsync(office.Manager));
        Assert.Contains(await ListAsync(office.Marketer), n => n.Kind == UserNotificationKinds.RepaymentReviewed && n.Title.Contains("disputed"));
    }

    [Fact]
    public async Task A_loan_application_reaches_its_approvers_then_its_raiser_and_the_disbursers()
    {
        var office = await SetUpOfficeAsync("application");
        int clientId, productId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = new Client { FirstName = "Ngozi", LastName = "Bell", OfficeId = office.OfficeId, Status = ClientStatus.Active, BiometricEnrolledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }

        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, "bell-application-admin@bckash.test", AdminPermissions);
        productId = (await (await admin.PostAsJsonAsync("/api/v1/loan-products", ProductRequest("Bell Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;

        var raised = await office.Marketer.PostAsJsonAsync("/api/v1/loan-applications",
            new CreateLoanApplicationRequest(LoanClientType.Client, null, null, office.OfficeId, clientId, null, productId, 12000, 12, FrequencyType.Months, null) with
            {
                DisbursementMode = DisbursementMode.CashPickup,
            });
        Assert.True(raised.StatusCode == HttpStatusCode.Created, await raised.Content.ReadAsStringAsync());
        var application = await raised.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        // Controllers in the office and directors over its zone approve loans; managers don't.
        Assert.Contains(await ListAsync(office.Controller), n => n.Kind == UserNotificationKinds.LoanApplicationPending && n.Title.Contains("Ngozi Bell"));
        Assert.Contains(await ListAsync(office.Director), n => n.Kind == UserNotificationKinds.LoanApplicationPending);
        Assert.DoesNotContain(await ListAsync(office.Manager), n => n.Kind == UserNotificationKinds.LoanApplicationPending);

        var approved = await office.Controller.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        // Dealt with by the controller, so it's closed for the director too.
        Assert.Equal(0, await OpenCountAsync(office.Director));
        Assert.Contains(await ListAsync(office.Marketer), n => n.Kind == UserNotificationKinds.LoanApplicationReviewed && n.Title.Contains("approved"));
        Assert.Contains(await ListAsync(office.Manager), n => n.Kind == UserNotificationKinds.LoanAwaitingDisbursement && !n.Done);
    }

    private sealed record Office(int OfficeId, HttpClient Manager, HttpClient Marketer, HttpClient Controller, HttpClient Director);

    /// <summary>An office (in its own zone) with a manager, a marketer, a controller, and a director over the zone.</summary>
    private async Task<Office> SetUpOfficeAsync(string label)
    {
        var emails = new[] { "manager", "marketer", "controller", "director" }.ToDictionary(r => r, r => $"bell-{label}-{r}@bckash.test");
        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            officeId = office.Id;
            await TestDataSeeder.SeedTypedUserAsync(db, emails["manager"], Password, UserTypeSlugs.Manager, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, emails["marketer"], Password, UserTypeSlugs.Marketer, officeId);
            await TestDataSeeder.SeedTypedUserAsync(db, emails["controller"], Password, UserTypeSlugs.Controller, officeId);
            var director = await TestDataSeeder.SeedTypedUserAsync(db, emails["director"], Password, UserTypeSlugs.Director, null);
            db.UserZones.Add(new UserZone { UserId = director.Id, ZoneId = office.ZoneId!.Value, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        return new Office(officeId, await SignInAsync(emails["manager"]), await SignInAsync(emails["marketer"]), await SignInAsync(emails["controller"]), await SignInAsync(emails["director"]));
    }

    /// <summary>A disbursed 12,000 loan (12 × 1,120) in <paramref name="officeId"/>, raised by a user with no user_type.</summary>
    private async Task<int> CreateDisbursedLoanAsync(int officeId, string label)
    {
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, $"bell-admin-{label.ToLowerInvariant()}@bckash.test", AdminPermissions);
        var productId = (await (await admin.PostAsJsonAsync("/api/v1/loan-products", ProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        int clientId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var client = new Client { FirstName = label, LastName = "Client", OfficeId = officeId, Status = ClientStatus.Active, BiometricEnrolledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }

        var application = await (await admin.PostAsJsonAsync("/api/v1/loan-applications",
            new CreateLoanApplicationRequest(LoanClientType.Client, null, null, officeId, clientId, null, productId, 12000, 12, FrequencyType.Months, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approved = await (await admin.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.True((await admin.PostAsJsonAsync($"/api/v1/loans/{approved!.LoanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null))).IsSuccessStatusCode);
        return approved.LoanId!.Value;
    }

    /// <summary>Repayments posted against the loan (its transactions also include the disbursement).</summary>
    private static async Task<int> RepaymentCountAsync(HttpClient staff, int loanId) =>
        (await staff.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/v1/loans/{loanId}/repayments", TestJson.Options) ?? [])
            .Count(t => t.TransactionType == LoanTransactionType.Repayment);

    private static async Task<int> OpenCountAsync(HttpClient staff) =>
        (await staff.GetFromJsonAsync<NotificationSummaryResponse>("/api/v1/notifications/summary", TestJson.Options))!.OpenCount;

    private static async Task<List<UserNotificationResponse>> ListAsync(HttpClient staff) =>
        await staff.GetFromJsonAsync<List<UserNotificationResponse>>("/api/v1/notifications", TestJson.Options) ?? [];

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static SaveLoanProductRequest ProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
        MinimumPrincipal: 1000, DefaultPrincipal: 12000, MaximumPrincipal: 20000,
        MinimumLoanTerm: 6, DefaultLoanTerm: 12, MaximumLoanTerm: 24,
        RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
        MinimumInterestRate: 1, DefaultInterestRate: 1, MaximumInterestRate: 1, InterestRateType: InterestRateFrequencyType.Month,
        GraceOnInterestCharged: null, GraceOnPrincipal: null, GraceOnInterestPayment: null,
        AllowCustomGrace: false, AllowStandingInstructions: false,
        InterestMethod: LoanInterestMethod.Flat, AmortizationMethod: LoanAmortizationMethod.EqualInstallment,
        InterestCalculationPeriodType: InterestCalculationPeriodType.Same, YearDays: YearDaysType.Days365, MonthDays: MonthDaysType.Days30,
        LoanTransactionStrategy: LoanTransactionStrategy.InterestPrincipalPenaltyFees,
        IncludeInCycle: false, LockGuarantee: false, AllocateOverpayments: false, AllowAdditionalCharges: false,
        AccountingRule: LoanAccountingRule.Cash, NpaDays: 90, ArrearsGraceDays: null, NpaSuspendIncome: true,
        GlAccountFundSourceId: null, GlAccountLoanPortfolioId: null, GlAccountReceivableInterestId: null, GlAccountReceivableFeeId: null,
        GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null, GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null,
        GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);
}
