using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

/// <summary>Acceptance criterion 4: a loan repayment funded from a savings account correctly debits savings and credits the loan in a single atomic operation.</summary>
public class SavingsTransfersControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsTransfersControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "loan-servicing.manage",
         "savings-products.manage", "savings-accounts.manage", "clients.manage", "gl.manage"];

    private static async Task<int> CreateGlAccountAsync(HttpClient client, string name, string code, GlAccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest(name, null, code, type, true, null));
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Repaying_a_loan_from_savings_debits_savings_and_credits_the_loan_atomically()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-transfer-repay@bckash.test", AllPermissions);

        var savingsControl = await CreateGlAccountAsync(client, "Savings Control", "2200", GlAccountType.Liability);
        var loanPortfolio = await CreateGlAccountAsync(client, "Loan Portfolio", "1100", GlAccountType.Asset);
        var incomeInterest = await CreateGlAccountAsync(client, "Income Interest", "4000", GlAccountType.Income);
        var fundSource = await CreateGlAccountAsync(client, "Fund Source", "2000", GlAccountType.Liability);

        var savingsProductRequest = new SaveSavingsProductRequest(
            Name: "Transfer Savings", ShortName: "TS", Description: null, CurrencyId: null, Decimals: 2,
            InterestRate: 0m, AllowOverdraft: false, MinimumBalance: 0m,
            InterestCompoundingPeriod: InterestCompoundingPeriod.Monthly, InterestPostingPeriod: InterestPostingPeriod.Monthly,
            InterestCalculationType: InterestCalculationType.Daily,
            AllowTransferWithdrawalFee: false, OpeningBalance: 0m, AllowAdditionalCharges: true,
            YearDays: SavingsYearDays.Days365, AccountingRule: SavingsAccountingRule.Cash,
            GlAccountSavingsReferenceId: savingsControl, GlAccountOverdraftPortfolioId: null, GlAccountSavingsControlId: savingsControl,
            GlAccountInterestOnSavingsId: null, GlAccountSavingsWrittenOffId: null, GlAccountIncomeInterestId: null,
            GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null);
        var savingsProduct = await (await client.PostAsJsonAsync("/api/savings-products", savingsProductRequest)).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);

        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, "Transfer", null, "Borrower", "Transfer Borrower",
            null, "Transfer Borrower", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        var savingsAccount = await (await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, borrower!.Id, null, null, savingsProduct!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        var approvedSavings = await (await client.PostAsJsonAsync($"/api/savings-accounts/{savingsAccount!.Id}/approve", new ApproveSavingsAccountRequest(5000m, null, new DateOnly(2026, 1, 1), null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);

        var loanProductRequest = new SaveLoanProductRequest(
            Name: "Transfer Loan Product", ShortName: "TLP", Description: null, FundId: null, CurrencyId: null, Decimals: 2,
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
            GlAccountFundSourceId: fundSource, GlAccountLoanPortfolioId: loanPortfolio, GlAccountReceivableInterestId: null,
            GlAccountReceivableFeeId: null, GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null,
            GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: incomeInterest, GlAccountIncomeFeeId: null,
            GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);
        var loanProduct = await (await client.PostAsJsonAsync("/api/loan-products", loanProductRequest)).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, borrower.Id, null, loanProduct!.Id, 12000m, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approvedApplication = await (await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000m, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approvedApplication!.LoanId!.Value;

        await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000m, null));

        // Repay installment 1 (1120 = 1000 principal + 120 interest) entirely from savings, in one call.
        var transferResponse = await client.PostAsJsonAsync("/api/savings-transfers/repay-loan", new RepayLoanFromSavingsRequest(approvedSavings!.Id, loanId, 1120m, new DateOnly(2026, 1, 28), "Repay from savings"));
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);

        // Savings side debited.
        var savingsAfter = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{approvedSavings.Id}", TestJson.Options);
        Assert.Equal(3880m, savingsAfter!.Balance); // 5000 - 1120

        // Loan side credited — schedule line 1 fully paid.
        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        var firstInstallment = schedule!.Single(s => s.Installment == 1);
        Assert.True(firstInstallment.Paid);
        Assert.Equal(1000m, firstInstallment.PrincipalPaid);
        Assert.Equal(120m, firstInstallment.InterestPaid);

        // One balanced GL batch covering both sides, traceable to both the savings account and the loan.
        var entries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/gl/journal-entries?glAccountId={savingsControl}", TestJson.Options);
        var savingsLeg = entries!.Single(e => e.SavingsId == approvedSavings.Id && e.LoanId == loanId);
        Assert.Equal(1120m, savingsLeg.Debit);

        var loanLegsResponse = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/gl/journal-entries?reference={savingsLeg.Reference}", TestJson.Options);
        var loanLegs = loanLegsResponse!;
        Assert.Equal(savingsLeg.Debit, loanLegs.Sum(e => e.Credit ?? 0m));
        Assert.All(loanLegs, e => Assert.Equal(loanId, e.LoanId));
    }
}
