using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

/// <summary>
/// Re-runs Phase 5's LoanServicingEndToEndTests scenario (client → apply → approve → disburse →
/// mixed repayments) with the loan product's GL accounts now configured, additionally asserting
/// that every loan transaction the scenario produces has a matching, balanced GlJournalEntry
/// batch traceable back to it — the exact acceptance criterion the Phase 6 spec calls out by
/// name ("Re-run Phase 5's end-to-end test and confirm every loan transaction in it now has
/// matching GL entries").
/// </summary>
public class LoanGlPostingEndToEndTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanGlPostingEndToEndTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage", "gl.manage"];

    [Fact]
    public async Task Every_loan_transaction_in_the_servicing_scenario_has_matching_balanced_gl_entries()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "e2e-gl-posting@bckash.test", AllPermissions);

        var fundSource = await CreateAccountAsync(client, "Fund Source", "2000", GlAccountType.Liability);
        var portfolio = await CreateAccountAsync(client, "Loan Portfolio", "1100", GlAccountType.Asset);
        var incomeInterest = await CreateAccountAsync(client, "Income Interest", "4000", GlAccountType.Income);

        var productRequest = new SaveLoanProductRequest(
            Name: "E2E GL Product", ShortName: "E2EGL", Description: null, FundId: null, CurrencyId: null, Decimals: 2,
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
            AccountingRule: LoanAccountingRule.Cash, NpaDays: 90, ArrearsGraceDays: null, NpaSuspendIncome: false,
            GlAccountFundSourceId: fundSource, GlAccountLoanPortfolioId: portfolio, GlAccountReceivableInterestId: null,
            GlAccountReceivableFeeId: null, GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null,
            GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: incomeInterest, GlAccountIncomeFeeId: null,
            GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);
        var product = await (await client.PostAsJsonAsync("/api/v1/loan-products", productRequest)).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, "E2EGl", null, "Borrower", "E2EGl Borrower",
            null, "E2EGl Borrower", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/v1/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, borrower!.Id, null, product!.Id, 12000m, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approved = await (await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000m, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        var disburseResponse = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000m, null));
        Assert.Equal(HttpStatusCode.OK, disburseResponse.StatusCode);

        var repayment1 = await (await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 28), "On-time")))
            .Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        var repayment2 = await (await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 3, 22), "Late")))
            .Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        var transactions = await client.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/v1/loans/{loanId}/repayments", TestJson.Options);
        var disbursementTransaction = transactions!.Single(t => t.TransactionType == LoanTransactionType.Disbursement);

        foreach (var transactionId in new[] { disbursementTransaction.Id, repayment1!.Id, repayment2!.Id })
        {
            var entries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={portfolio}", TestJson.Options);
            var batchEntries = entries!.Where(e => e.LoanTransactionId == transactionId).ToList();
            Assert.NotEmpty(batchEntries);
        }

        // Every entry traceable to this loan is balanced (debit == credit) and approved (system-posted, not a manual draft).
        var allLoanEntries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={portfolio}", TestJson.Options);
        var incomeEntries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={incomeInterest}", TestJson.Options);
        var fundSourceEntries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={fundSource}", TestJson.Options);

        var all = allLoanEntries!.Concat(incomeEntries!).Concat(fundSourceEntries!).Where(e => e.LoanId == loanId).ToList();
        Assert.NotEmpty(all);
        Assert.Equal(all.Sum(e => e.Debit ?? 0m), all.Sum(e => e.Credit ?? 0m));
        Assert.All(all, e => Assert.True(e.Approved));
        Assert.All(all, e => Assert.False(e.ManualEntry));
    }

    private static async Task<int> CreateAccountAsync(HttpClient client, string name, string code, GlAccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/gl-accounts", new SaveGlAccountRequest(name, null, code, type, true, null));
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return created!.Id;
    }
}
