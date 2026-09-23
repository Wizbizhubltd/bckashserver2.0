using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

public class GlReportsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GlReportsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage", "gl.manage"];

    [Fact]
    public async Task Trial_balance_nets_to_zero_across_disbursement_writeoff_and_a_manual_entry()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-trial-balance@bckash.test", AllPermissions);

        var fundSource = await CreateAccountAsync(client, "Fund Source", "2000", GlAccountType.Liability);
        var portfolio = await CreateAccountAsync(client, "Loan Portfolio", "1100", GlAccountType.Asset);
        var writtenOff = await CreateAccountAsync(client, "Loans Written Off", "5100", GlAccountType.Expense);
        var receivableInterest = await CreateAccountAsync(client, "Receivable Interest", "1200", GlAccountType.Asset);
        var cash = await CreateAccountAsync(client, "Cash", "1000", GlAccountType.Asset);
        var otherIncome = await CreateAccountAsync(client, "Other Income", "4100", GlAccountType.Income);

        var productRequest = new SaveLoanProductRequest(
            Name: "GL Report Product", ShortName: "GLR", Description: null, FundId: null, CurrencyId: null, Decimals: 2,
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
            GlAccountFundSourceId: fundSource, GlAccountLoanPortfolioId: portfolio, GlAccountReceivableInterestId: receivableInterest,
            GlAccountReceivableFeeId: null, GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null,
            GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null, GlAccountIncomeFeeId: null,
            GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: writtenOff);
        var product = await (await client.PostAsJsonAsync("/api/loan-products", productRequest)).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, "GlReport", null, "Borrower", "GlReport Borrower",
            null, "GlReport Borrower", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, borrower!.Id, null, product!.Id, 12000m, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approved = await (await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000m, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        // Disbursement posts Debit Portfolio / Credit Fund Source.
        await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000m, null));

        // Write off the entire outstanding schedule — posts Debit Written Off / Credit Portfolio + Receivable Interest.
        await client.PostAsJsonAsync($"/api/loans/{loanId}/write-off", new WriteOffLoanRequest("Uncollectable", new DateOnly(2026, 6, 1)));

        // An unrelated, independently-balanced manual entry.
        var manualResponse = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            null, new DateOnly(2026, 6, 2), [new JournalEntryLineRequest(cash, 200m, null), new JournalEntryLineRequest(otherIncome, null, 200m)], "Misc"));
        var manualEntries = await manualResponse.Content.ReadFromJsonAsync<List<GlJournalEntryResponse>>(TestJson.Options);
        await client.PostAsJsonAsync($"/api/gl/journal-entries/{manualEntries![0].Reference}/approve", new ApproveJournalEntryRequest(null));

        var trialBalanceResponse = await client.GetFromJsonAsync<List<TrialBalanceRowResponse>>("/api/gl/reports/trial-balance", TestJson.Options);
        var trialBalance = trialBalanceResponse!;
        Assert.NotEmpty(trialBalance);
        Assert.Equal(trialBalance.Sum(r => r.TotalDebit), trialBalance.Sum(r => r.TotalCredit));

        // The write-off produced a matching, balanced batch traceable back to the loan.
        var writeOffEntries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/gl/journal-entries?glAccountId={writtenOff}", TestJson.Options);
        Assert.NotEmpty(writeOffEntries!);
        Assert.All(writeOffEntries!, e => Assert.Equal(loanId, e.LoanId));
    }

    private static async Task<int> CreateAccountAsync(HttpClient client, string name, string code, GlAccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest(name, null, code, type, true, null));
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return created!.Id;
    }
}
