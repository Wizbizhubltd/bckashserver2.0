using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoansControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoansControllerTests(BCKashWebApplicationFactory factory)
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

    private static async Task<int> CreateClientAsync(HttpClient client, string label)
    {
        var request = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    private static async Task<int> CreateApprovedLoanAsync(HttpClient client, string label)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/loan-products", NewProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 5000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(4500, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        return approved!.LoanId!.Value;
    }

    [Fact]
    public async Task Request_changes_succeeds_from_pending_and_moves_the_loan_to_need_changes()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-request-changes@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "RequestChanges");

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/request-changes", new ReasonRequest("Please clarify collateral value"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(TestJson.Options);
        Assert.Equal(LoanStatus.NeedChanges, loan!.Status);
        Assert.NotNull(loan.NeedChangesById);
        Assert.NotNull(loan.NeedChangesDate);
    }

    [Fact]
    public async Task Request_changes_fails_when_not_pending()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-request-changes-invalid@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "RequestChangesInvalid");
        await client.PostAsJsonAsync($"/api/loans/{loanId}/request-changes", new ReasonRequest("First request"));

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/request-changes", new ReasonRequest("Second request"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_succeeds_from_need_changes_and_fails_from_pending()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-resubmit@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "Resubmit");

        var prematureResubmit = await client.PostAsync($"/api/loans/{loanId}/resubmit", null);
        Assert.Equal(HttpStatusCode.BadRequest, prematureResubmit.StatusCode);

        await client.PostAsJsonAsync($"/api/loans/{loanId}/request-changes", new ReasonRequest("Needs revision"));

        var resubmitResponse = await client.PostAsync($"/api/loans/{loanId}/resubmit", null);
        Assert.Equal(HttpStatusCode.OK, resubmitResponse.StatusCode);
        var loan = await resubmitResponse.Content.ReadFromJsonAsync<LoanResponse>(TestJson.Options);
        Assert.Equal(LoanStatus.Pending, loan!.Status);
    }

    [Fact]
    public async Task Request_changes_and_resubmit_use_different_permissions()
    {
        var setupClient = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-perm-setup@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var loanId = await CreateApprovedLoanAsync(setupClient, "PermSplit");

        var manageOnlyClient = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-perm-manage@bckash.test", "loan-applications.manage");
        var forbiddenRequestChanges = await manageOnlyClient.PostAsJsonAsync($"/api/loans/{loanId}/request-changes", new ReasonRequest("Nope"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRequestChanges.StatusCode);

        var approveOnlyClient = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-perm-approve@bckash.test", "loan-applications.approve");
        var forbiddenResubmit = await approveOnlyClient.PostAsync($"/api/loans/{loanId}/resubmit", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResubmit.StatusCode);
    }

    [Fact]
    public async Task Disburse_succeeds_from_pending_and_generates_the_repayment_schedule()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-disburse@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "Disburse");

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(null, 4500, "Cash handed over"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(TestJson.Options);
        Assert.Equal(LoanStatus.Disbursed, loan!.Status);
        Assert.Equal(4500, loan.Principal);
        Assert.NotNull(loan.DisbursementDate);
        Assert.NotNull(loan.DisbursedById);
        Assert.Equal("Cash handed over", loan.DisbursedNotes);

        // Disbursement now generates the schedule (12 monthly installments, per CreateApprovedLoanAsync's application).
        var scheduleResponse = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        Assert.Equal(12, scheduleResponse!.Count);
        Assert.Equal(4500m, scheduleResponse.Sum(s => s.Principal ?? 0m));
        Assert.All(scheduleResponse, s => Assert.False(s.Paid));

        // A Disbursement transaction was also recorded.
        var transactions = await client.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/loans/{loanId}/repayments", TestJson.Options);
        Assert.Contains(transactions!, t => t.TransactionType == LoanTransactionType.Disbursement && t.Amount == 4500m);
    }

    [Fact]
    public async Task Disburse_fails_when_the_loan_is_not_pending()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-disburse-invalid@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "DisburseInvalid");
        await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(null, 4500, null));

        var secondDisburse = await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(null, 4500, null));

        Assert.Equal(HttpStatusCode.BadRequest, secondDisburse.StatusCode);
    }

    [Fact]
    public async Task Disburse_requires_the_loan_servicing_permission_not_just_manage_or_approve()
    {
        var setupClient = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-disburse-perm-setup@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var loanId = await CreateApprovedLoanAsync(setupClient, "DisbursePerm");

        var forbidden = await setupClient.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(null, 4500, null));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var servicingClient = await AuthenticatedClientFactory.CreateAsync(_factory, "loan-disburse-perm-servicing@bckash.test", "loan-servicing.manage");
        var allowed = await servicingClient.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(null, 4500, null));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
