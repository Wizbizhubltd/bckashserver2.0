using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanApplicationsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanApplicationsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    internal static SaveLoanProductRequest NewProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
        MinimumPrincipal: 1000, DefaultPrincipal: 5000, MaximumPrincipal: 10000,
        MinimumLoanTerm: 6, DefaultLoanTerm: 12, MaximumLoanTerm: 24,
        RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
        MinimumInterestRate: 10, DefaultInterestRate: 15, MaximumInterestRate: 20, InterestRateType: InterestRateFrequencyType.Year,
        GraceOnInterestCharged: null, GraceOnPrincipal: null, GraceOnInterestPayment: null,
        AllowCustomGrace: false, AllowStandingInstructions: false,
        InterestMethod: LoanInterestMethod.Flat, AmortizationMethod: LoanAmortizationMethod.EqualInstallment,
        InterestCalculationPeriodType: InterestCalculationPeriodType.Same, YearDays: YearDaysType.Days365, MonthDays: MonthDaysType.Days30,
        LoanTransactionStrategy: LoanTransactionStrategy.InterestPrincipalPenaltyFees,
        IncludeInCycle: false, LockGuarantee: false, AllocateOverpayments: false, AllowAdditionalCharges: false,
        AccountingRule: LoanAccountingRule.Cash, NpaDays: null, ArrearsGraceDays: null, NpaSuspendIncome: false,
        GlAccountFundSourceId: null, GlAccountLoanPortfolioId: null, GlAccountReceivableInterestId: null, GlAccountReceivableFeeId: null,
        GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null, GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null,
        GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);

    internal static async Task<int> CreateProductAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/loan-products", NewProductRequest(name));
        var created = await response.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        return created!.Id;
    }

    internal static async Task<int> CreateClientAsync(HttpClient client, string label)
    {
        var request = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    internal static CreateLoanApplicationRequest NewApplicationRequest(int productId, int clientId, decimal amount = 5000, int? term = 12) =>
        new(LoanClientType.Client, null, null, null, clientId, null, productId, amount, term, FrequencyType.Months, null);

    [Fact]
    public async Task Create_and_fetch_a_loan_application()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-crud@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Application Product");
        var clientId = await CreateClientAsync(client, "Applicant");

        var response = await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(ApprovalStatus.Pending, created!.Status);
        Assert.Null(created.LoanId);
    }

    [Fact]
    public async Task Create_rejects_an_amount_outside_the_products_range()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-amount-range@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Range Product");
        var clientId = await CreateClientAsync(client, "RangeApplicant");

        var response = await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId, amount: 50000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_a_term_outside_the_products_range()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-term-range@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Term Range Product");
        var clientId = await CreateClientAsync(client, "TermApplicant");

        var response = await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId, term: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_creates_a_pending_loan_and_links_it_back_to_the_application()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-approve@bckash.test", ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Approve Product");
        var clientId = await CreateClientAsync(client, "ApproveApplicant");
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        var approveResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(4500, "Looks good"));

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(ApprovalStatus.Approved, approved!.Status);
        Assert.NotNull(approved.LoanId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var loan = await db.Loans.FirstAsync(l => l.Id == approved.LoanId);
        Assert.Equal(LoanStatus.Pending, loan.Status);
        Assert.Equal(4500, loan.ApprovedAmount);
        Assert.Matches("^LN[0-9]{8}$", loan.AccountNumber!);

        var reloadedApplication = await db.LoanApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(loan.Id, reloadedApplication.LoanId);
    }

    [Fact]
    public async Task Decline_is_terminal_a_second_transition_attempt_fails()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-decline@bckash.test", ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Decline Product");
        var clientId = await CreateClientAsync(client, "DeclineApplicant");
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        var declineResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/decline", new ReasonRequest("Insufficient collateral"));
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);

        var secondDecline = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application.Id}/decline", new ReasonRequest("Trying again"));
        Assert.Equal(HttpStatusCode.BadRequest, secondDecline.StatusCode);

        var approveAfterDecline = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application.Id}/approve", new ApproveLoanApplicationRequest(1000, null));
        Assert.Equal(HttpStatusCode.BadRequest, approveAfterDecline.StatusCode);
    }

    [Fact]
    public async Task Manage_and_approve_are_genuinely_separate_permissions()
    {
        var manageOnlyClient = await AuthenticatedClientFactory.CreateAsync(_factory, "application-manage-only@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(manageOnlyClient, "Permission Product");
        var clientId = await CreateClientAsync(manageOnlyClient, "PermissionApplicant");
        var application = await (await manageOnlyClient.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId))).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        // manage-only user cannot approve.
        var forbiddenApprove = await manageOnlyClient.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(1000, null));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApprove.StatusCode);

        var approveOnlyClient = await AuthenticatedClientFactory.CreateAsync(_factory, "application-approve-only@bckash.test", "loan-applications.approve");

        // approve-only user cannot create.
        var forbiddenCreate = await approveOnlyClient.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, clientId));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        // approve-only user can approve the application the manage-only user created.
        var approveResponse = await approveOnlyClient.PostAsJsonAsync($"/api/v1/loan-applications/{application.Id}/approve", new ApproveLoanApplicationRequest(1000, null));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
    }

    [Fact]
    public async Task A_super_admin_can_approve_and_decline_applications()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "application-sa-staff@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(staff, "Super Admin Product");
        var toApprove = await (await staff.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, await CreateClientAsync(staff, "SaApprove"))))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var toDecline = await (await staff.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, await CreateClientAsync(staff, "SaDecline"))))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "application-sa@bckash.test");
        await MakeEligibleAsync(toApprove!.ClientId!.Value);

        var approveResponse = await superAdmin.PostAsJsonAsync($"/api/v1/loan-applications/{toApprove!.Id}/approve", new ApproveLoanApplicationRequest(4000, "Approved centrally"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.NotNull((await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options))!.LoanId);

        var declineResponse = await superAdmin.PostAsJsonAsync($"/api/v1/loan-applications/{toDecline!.Id}/decline", new ReasonRequest("Over exposure limit"));
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        Assert.Equal(ApprovalStatus.Declined, (await declineResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options))!.Status);
    }

    /// <summary>Approves the client and enrolls their face, as staff must before a loan is approved for them.</summary>
    private async Task MakeEligibleAsync(int clientId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var client = (await db.Clients.FindAsync(clientId))!;
        client.Status = ClientStatus.Active;
        client.BiometricEnrolledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_super_admin_cannot_approve_a_loan_for_a_client_who_went_back_to_pending()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "application-sa-pending-staff@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(staff, "Pending Again Product");
        var application = await (await staff.PostAsJsonAsync("/api/v1/loan-applications", NewApplicationRequest(productId, await CreateClientAsync(staff, "PendingAgain"))))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "application-sa-pending@bckash.test");

        var refused = await superAdmin.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(4000, null));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("pending approval", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_bank_transfer_needs_complete_bank_details_and_they_carry_onto_the_loan()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-payout@bckash.test", ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Payout Product");
        var clientId = await CreateClientAsync(client, "PayoutApplicant");
        var request = NewApplicationRequest(productId, clientId) with
        {
            DisbursementMode = DisbursementMode.BankTransfer,
            DisbursementBankName = "Access Bank",
            DisbursementAccountNumber = "01234",
            DisbursementAccountName = "Payout Applicant",
        };

        var shortNumber = await client.PostAsJsonAsync("/api/v1/loan-applications", request);
        Assert.Equal(HttpStatusCode.BadRequest, shortNumber.StatusCode);
        Assert.Contains("10 digits", await shortNumber.Content.ReadAsStringAsync());

        var noBank = await client.PostAsJsonAsync("/api/v1/loan-applications", request with { DisbursementBankName = " ", DisbursementAccountNumber = "0123456789" });
        Assert.Equal(HttpStatusCode.BadRequest, noBank.StatusCode);

        var created = await client.PostAsJsonAsync("/api/v1/loan-applications", request with { DisbursementAccountNumber = "0123456789" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var application = await created.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(DisbursementMode.BankTransfer, application!.DisbursementMode);
        Assert.Equal("0123456789", application.DisbursementAccountNumber);

        var approved = await (await client.PostAsJsonAsync($"/api/v1/loan-applications/{application.Id}/approve", new ApproveLoanApplicationRequest(5000, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loan = await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{approved!.LoanId}", TestJson.Options);
        Assert.Equal(DisbursementMode.BankTransfer, loan!.DisbursementMode);
        Assert.Equal("Access Bank", loan.DisbursementBankName);
        Assert.Equal("0123456789", loan.DisbursementAccountNumber);
        Assert.Equal("Payout Applicant", loan.DisbursementAccountName);
    }

    [Fact]
    public async Task Cash_and_cheque_pickups_keep_no_bank_details()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "application-cash@bckash.test", ["loan-applications.manage", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Cash Product");
        var request = NewApplicationRequest(productId, await CreateClientAsync(client, "CashApplicant")) with
        {
            DisbursementMode = DisbursementMode.CashPickup,
            DisbursementBankName = "Ignored Bank",
            DisbursementAccountNumber = "123",
        };

        var created = await client.PostAsJsonAsync("/api/v1/loan-applications", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var application = await created.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(DisbursementMode.CashPickup, application!.DisbursementMode);
        Assert.Null(application.DisbursementBankName);
        Assert.Null(application.DisbursementAccountNumber);
    }

    [Fact]
    public async Task Staff_raising_a_loan_must_say_how_it_will_be_disbursed()
    {
        var admin = await AuthenticatedClientFactory.CreateAsync(_factory, "application-mode-admin@bckash.test", ["loan-products.manage"]);
        var productId = await CreateProductAsync(admin, "Mode Product");
        const string marketerEmail = "application-mode-marketer@bckash.test";
        int clientId, officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            officeId = office.Id;
            await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, "Correct-Password1!", UserTypeSlugs.Marketer, office.Id);
            var applicant = new Client { FirstName = "Mode", LastName = "Applicant", OfficeId = office.Id, Status = ClientStatus.Active, BiometricEnrolledAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.Clients.Add(applicant);
            await db.SaveChangesAsync();
            clientId = applicant.Id;
        }

        var marketer = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, marketer, marketerEmail, "Correct-Password1!");
        marketer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var inOffice = NewApplicationRequest(productId, clientId) with { OfficeId = officeId };
        var noMode = await marketer.PostAsJsonAsync("/api/v1/loan-applications", inOffice);
        var noModeBody = await noMode.Content.ReadAsStringAsync();
        Assert.True(noMode.StatusCode == HttpStatusCode.BadRequest, $"{(int)noMode.StatusCode}: {noModeBody}");
        Assert.Contains("how the loan will be disbursed", noModeBody);

        var chequePickup = await marketer.PostAsJsonAsync("/api/v1/loan-applications", inOffice with { DisbursementMode = DisbursementMode.ChequePickup });
        Assert.True(chequePickup.StatusCode == HttpStatusCode.Created, await chequePickup.Content.ReadAsStringAsync());
    }
}
