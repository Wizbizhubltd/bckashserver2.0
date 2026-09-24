using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
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

    private static SaveLoanProductRequest NewProductRequest(string name) => new(
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

    private static async Task<int> CreateProductAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/loan-products", NewProductRequest(name));
        var created = await response.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        return created!.Id;
    }

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

    private static CreateLoanApplicationRequest NewApplicationRequest(int productId, int clientId, decimal amount = 5000, int? term = 12) =>
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
}
