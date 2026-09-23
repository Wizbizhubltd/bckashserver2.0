using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanNpaControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanNpaControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static SaveLoanProductRequest ProductRequest(string name, int? npaDays, bool npaSuspendIncome) => new(
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
        AccountingRule: LoanAccountingRule.Cash, NpaDays: npaDays, ArrearsGraceDays: null, NpaSuspendIncome: npaSuspendIncome,
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

    private static async Task<int> CreateDisbursedLoanAsync(HttpClient client, string label, DateOnly disbursementDate, int? npaDays, bool npaSuspendIncome)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/loan-products", ProductRequest($"{label} Product", npaDays, npaSuspendIncome))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(disbursementDate, 12000, null));
        return loanId;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    [Fact]
    public async Task A_loan_with_arrears_beyond_npa_days_is_flagged_and_suspends_income()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "npa-flagged@bckash.test", AllPermissions);

        // Disbursed 13 months ago, with no repayments — installment 1 (due ~12 months ago) is
        // guaranteed to be well past a 90-day arrears threshold regardless of when this test runs.
        var disbursementDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-13);
        var loanId = await CreateDisbursedLoanAsync(client, "NpaFlagged", disbursementDate, npaDays: 90, npaSuspendIncome: true);

        var response = await client.PostAsync($"/api/loans/{loanId}/recompute-npa", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<NpaStatusResponse>(TestJson.Options);
        Assert.True(status!.IsNpa);
        Assert.True(status.IncomeSuspended);
        Assert.True(status.DaysInArrears > 90);

        var loan = await client.GetFromJsonAsync<LoanResponse>($"/api/loans/{loanId}", TestJson.Options);
        Assert.True(loan!.IsNpa);
        Assert.True(loan.IncomeSuspended);
    }

    [Fact]
    public async Task A_loan_with_no_overdue_installments_is_not_flagged_NPA()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "npa-clean@bckash.test", AllPermissions);

        // Disbursed today — no installment is due yet, let alone overdue.
        var disbursementDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var loanId = await CreateDisbursedLoanAsync(client, "NpaClean", disbursementDate, npaDays: 90, npaSuspendIncome: true);

        var response = await client.PostAsync($"/api/loans/{loanId}/recompute-npa", null);
        var status = await response.Content.ReadFromJsonAsync<NpaStatusResponse>(TestJson.Options);

        Assert.False(status!.IsNpa);
        Assert.False(status.IncomeSuspended);
    }

    [Fact]
    public async Task Income_is_not_suspended_when_the_product_doesnt_configure_it_even_if_NPA()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "npa-no-suspend@bckash.test", AllPermissions);

        var disbursementDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-13);
        var loanId = await CreateDisbursedLoanAsync(client, "NpaNoSuspend", disbursementDate, npaDays: 90, npaSuspendIncome: false);

        var response = await client.PostAsync($"/api/loans/{loanId}/recompute-npa", null);
        var status = await response.Content.ReadFromJsonAsync<NpaStatusResponse>(TestJson.Options);

        Assert.True(status!.IsNpa);
        Assert.False(status.IncomeSuspended);
    }
}
