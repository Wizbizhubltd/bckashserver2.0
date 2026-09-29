using System.Net;
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

public class LoanRepaymentsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanRepaymentsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // P=12000, 1%/month, 12 installments, flat/equal-installment — matches docs/interest-calculation-spec.md's
    // Example 1 exactly: every installment is Principal=1000.00, Interest=120.00, Total=1120.00.
    private static SaveLoanProductRequest CleanMonthlyProductRequest(string name) => new(
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

    private static async Task<int> CreateClientAsync(HttpClient client, string label)
    {
        var request = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    /// <summary>Produces a Disbursed loan with the clean 12×(1000 principal, 120 interest) schedule described above.</summary>
    private async Task<int> CreateDisbursedLoanAsync(HttpClient client, string label, int? officeId = null)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/v1/loan-products", CleanMonthlyProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, officeId, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null));
        await TestDataSeeder.WithoutSavingsAsync(_factory, loanId);
        return loanId;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    [Fact]
    public async Task On_time_repayment_fully_covers_the_first_installment()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-ontime@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "OnTime");

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), "First payment"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(1000m, transaction!.Principal);
        Assert.Equal(120m, transaction.Interest);
        Assert.Equal(LoanTransactionType.Repayment, transaction.TransactionType);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.True(first.Paid);
        Assert.Equal(1000m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);

        var second = schedule!.Single(s => s.Installment == 2);
        Assert.False(second.Paid);
        Assert.Equal(0m, second.PrincipalPaid ?? 0m);
    }

    [Fact]
    public async Task Partial_repayment_allocates_interest_before_principal_and_leaves_the_installment_unpaid()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-partial@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Partial");

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(500m, null, new DateOnly(2026, 1, 15), "Partial payment"));
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        // Strategy is InterestPrincipalPenaltyFees: interest (120) fully covered first, remaining 380 to principal.
        Assert.Equal(120m, transaction!.Interest);
        Assert.Equal(380m, transaction.Principal);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.False(first.Paid);
        Assert.Equal(380m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);
    }

    [Fact]
    public async Task Late_repayment_still_allocates_correctly_and_is_recorded_as_paid_late()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-late@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Late");

        // Installment 1 is due 2026-02-01; pay well after that date.
        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 2, 20), "Late payment"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.True(first.Paid);
        Assert.Equal(1000m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);
    }

    [Fact]
    public async Task A_repayment_can_never_exceed_what_is_still_owed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-overpay@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Overpay");

        // Total outstanding across all 12 installments is 12 x 1120 = 13440; a cent more is refused.
        var tooMuch = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(13440.01m, null, new DateOnly(2026, 1, 15), null));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("At most ₦13,440 can be paid", await tooMuch.Content.ReadAsStringAsync());
        Assert.Equal(13440m, (await client.GetFromJsonAsync<RepaymentAccountResponse>($"/api/v1/loans/{loanId}/repayments/pay-into", TestJson.Options))!.MaxRepayment);

        // Exactly what's owed clears the loan…
        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(13440m, null, new DateOnly(2026, 1, 15), null));
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(13440m, transaction!.Principal + transaction.Interest);
        Assert.Null(transaction.Overpayment);
        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.All(schedule!, s => Assert.True(s.Paid));

        // …and closes it out as completed, after which nothing more is taken.
        var summary = await client.GetFromJsonAsync<LoanSummaryResponse>($"/api/v1/loans/{loanId}/summary", TestJson.Options);
        Assert.True(summary!.Completion!.Completed);
        Assert.Equal(LoanStatus.Closed, (await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options))!.Status);
        var afterPaidOff = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1m, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, afterPaidOff.StatusCode);
        Assert.Contains("fully repaid", await afterPaidOff.Content.ReadAsStringAsync());

        Assert.Equal(0m, (await client.GetFromJsonAsync<RepaymentAccountResponse>($"/api/v1/loans/{loanId}/repayments/pay-into", TestJson.Options))!.MaxRepayment);
        // Reversing the repayment leaves money owed again, so the loan reopens.
        Assert.True((await client.PostAsync($"/api/v1/loans/{loanId}/repayments/{transaction.Id}/reverse", null)).IsSuccessStatusCode);
        Assert.Equal(LoanStatus.Disbursed, (await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options))!.Status);
    }

    [Fact]
    public async Task Reversing_a_repayment_restores_the_schedule_to_its_exact_pre_transaction_state()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-reverse@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Reverse");

        var beforeSchedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null));
        var transaction = await payResponse.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        var afterPayment = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.True(afterPayment!.Single(s => s.Installment == 1).Paid);

        var reverseResponse = await client.PostAsync($"/api/v1/loans/{loanId}/repayments/{transaction!.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversedTransaction = await reverseResponse.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.True(reversedTransaction!.Reversed);

        var afterReversal = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);

        // Exact pre-transaction state restored.
        for (var i = 0; i < beforeSchedule!.Count; i++)
        {
            Assert.Equal(beforeSchedule[i].Paid, afterReversal![i].Paid);
            Assert.Equal(beforeSchedule[i].PrincipalPaid ?? 0m, afterReversal[i].PrincipalPaid ?? 0m);
            Assert.Equal(beforeSchedule[i].InterestPaid ?? 0m, afterReversal[i].InterestPaid ?? 0m);
        }

        // Reversing again is rejected.
        var secondReverse = await client.PostAsync($"/api/v1/loans/{loanId}/repayments/{transaction.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondReverse.StatusCode);
    }

    [Fact]
    public async Task Repayment_is_rejected_for_a_loan_that_hasnt_been_disbursed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-not-disbursed@bckash.test", AllPermissions);
        var productId = (await (await client.PostAsJsonAsync("/api/v1/loan-products", CleanMonthlyProductRequest("NotDisbursed Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, "NotDisbursed");
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{approved!.LoanId}/repayments", new RecordRepaymentRequest(1120m, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_loan_with_a_penalty_still_owed_stays_open_and_says_why()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-penalty-open@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "PenaltyOpen");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var first = await db.LoanRepaymentSchedules.Where(s => s.LoanId == loanId).OrderBy(s => s.DueDate).FirstAsync();
            first.Penalty = 50m;
            await db.SaveChangesAsync();
        }

        // Principal and interest (13,440) are paid; the 50 penalty isn't.
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(13440m, null, new DateOnly(2026, 1, 15), null));

        var summary = await client.GetFromJsonAsync<LoanSummaryResponse>($"/api/v1/loans/{loanId}/summary", TestJson.Options);
        Assert.False(summary!.Completion!.Completed);
        Assert.Equal(50m, summary.Completion.PrincipalOwed + summary.Completion.InterestOwed + summary.Completion.FeesOwed + summary.Completion.PenaltyOwed);
        Assert.Equal(LoanStatus.Disbursed, (await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options))!.Status);

        // Paying it completes the loan.
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(50m, null, new DateOnly(2026, 1, 20), null));
        Assert.Equal(LoanStatus.Closed, (await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options))!.Status);
    }

    [Fact]
    public async Task Loans_already_fully_repaid_are_closed_out_by_the_sweep()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-sweep@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Sweep");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            // Repaid before loans closed themselves: every instalment paid, the loan still marked Disbursed.
            foreach (var s in await db.LoanRepaymentSchedules.Where(s => s.LoanId == loanId).ToListAsync())
            {
                (s.PrincipalPaid, s.InterestPaid, s.Paid) = (s.Principal, s.Interest, true);
            }

            await db.SaveChangesAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<BCKash.Application.Loans.ILoanCompletionService>().SweepAsync() >= 1);
        }

        Assert.Equal(LoanStatus.Closed, (await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options))!.Status);
    }

    [Fact]
    public async Task A_marketer_records_repayments_in_their_own_office_and_sees_where_to_pay()
    {
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-marketer-admin@bckash.test", AllPermissions);
        int officeId, otherOfficeId;
        const string marketerEmail = "repayment-marketer@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            otherOfficeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, "Correct-Password1!", UserTypeSlugs.Marketer, officeId);
            db.OfficeBankAccounts.AddRange(
                new OfficeBankAccount { OfficeId = officeId, BankName = "Old Bank", AccountName = "BCKash Old", AccountNumber = "1111111111", IsDefault = false, Active = true },
                new OfficeBankAccount { OfficeId = officeId, BankName = "Access Bank", AccountName = "BCKash Ikeja", AccountNumber = "0123456789", IsDefault = true, Active = true });
            await db.SaveChangesAsync();
        }

        var ownLoanId = await CreateDisbursedLoanAsync(admin, "MarketerOwn", officeId);
        var otherLoanId = await CreateDisbursedLoanAsync(admin, "MarketerOther", otherOfficeId);

        var marketer = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, marketer, marketerEmail, "Correct-Password1!");
        marketer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var payInto = await marketer.GetFromJsonAsync<RepaymentAccountResponse>($"/api/v1/loans/{ownLoanId}/repayments/pay-into", TestJson.Options);
        Assert.Equal(new RepaymentAccountResponse(payInto!.OfficeName, "Access Bank", "BCKash Ikeja", "0123456789", MaxRepayment: 13440m), payInto);

        // A marketer's repayment waits for the office manager's confirmation.
        var recorded = await marketer.PostAsJsonAsync($"/api/v1/loans/{ownLoanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null));
        Assert.Equal(HttpStatusCode.Accepted, recorded.StatusCode);
        Assert.Equal(RepaymentSubmissionStatus.Pending, (await recorded.Content.ReadFromJsonAsync<RepaymentSubmissionResponse>(TestJson.Options))!.Status);

        // Recording is all a marketer does: reversing a posted repayment still needs Service loans.
        var posted = await (await admin.PostAsJsonAsync($"/api/v1/loans/{ownLoanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null)))
            .Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, (await marketer.PostAsync($"/api/v1/loans/{ownLoanId}/repayments/{posted!.Id}/reverse", null)).StatusCode);

        // Another office's loans stay out of reach.
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.PostAsJsonAsync($"/api/v1/loans/{otherLoanId}/repayments", new RecordRepaymentRequest(1120m, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marketer.GetAsync($"/api/v1/loans/{otherLoanId}/repayments/pay-into")).StatusCode);
    }
}
