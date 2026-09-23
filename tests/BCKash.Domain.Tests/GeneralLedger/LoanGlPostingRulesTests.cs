using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Domain.Tests.GeneralLedger;

/// <summary>
/// Every LoanGlPostingRules method must return balanced lines (sum debit == sum credit) or an
/// empty list when the product isn't fully configured for GL — see docs/gl-posting-spec.md.
/// </summary>
public class LoanGlPostingRulesTests
{
    private static readonly LoanProduct FullyConfigured = new()
    {
        GlAccountFundSourceId = 1,
        GlAccountLoanPortfolioId = 2,
        GlAccountReceivableInterestId = 3,
        GlAccountReceivableFeeId = 4,
        GlAccountReceivablePenaltyId = 5,
        GlAccountLoanOverPaymentsId = 6,
        GlAccountSuspendedIncomeId = 7,
        GlAccountIncomeInterestId = 8,
        GlAccountIncomeFeeId = 9,
        GlAccountIncomePenaltyId = 10,
        GlAccountIncomeRecoveryId = 11,
        GlAccountLoansWrittenOffId = 12,
    };

    private static void AssertBalanced(IReadOnlyList<GlPostingLine> lines)
    {
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Sum(l => l.Debit ?? 0m), lines.Sum(l => l.Credit ?? 0m));
    }

    [Fact]
    public void Disbursement_debits_portfolio_credits_fund_source()
    {
        var lines = LoanGlPostingRules.ForDisbursement(FullyConfigured, 1000m);
        AssertBalanced(lines);
        Assert.Equal(2, lines.Count);
        Assert.Equal(1000m, lines.Single(l => l.GlAccountId == 2).Debit);
        Assert.Equal(1000m, lines.Single(l => l.GlAccountId == 1).Credit);
    }

    [Fact]
    public void Disbursement_with_unmapped_account_produces_no_lines()
    {
        var product = new LoanProduct { GlAccountLoanPortfolioId = 2 }; // fund source missing
        Assert.Empty(LoanGlPostingRules.ForDisbursement(product, 1000m));
    }

    [Fact]
    public void Repayment_with_all_components_balances_and_routes_each_component()
    {
        var lines = LoanGlPostingRules.ForRepayment(FullyConfigured, principal: 1000m, interest: 120m, fees: 50m, penalty: 25m, overpayment: 5m);
        AssertBalanced(lines);
        Assert.Equal(1200m, lines.Single(l => l.GlAccountId == 1).Debit); // fund source, total
        Assert.Equal(1000m, lines.Single(l => l.GlAccountId == 2).Credit); // loan portfolio, principal
        Assert.Equal(120m, lines.Single(l => l.GlAccountId == 8).Credit); // income interest
        Assert.Equal(50m, lines.Single(l => l.GlAccountId == 9).Credit); // income fee
        Assert.Equal(25m, lines.Single(l => l.GlAccountId == 10).Credit); // income penalty
        Assert.Equal(5m, lines.Single(l => l.GlAccountId == 6).Credit); // overpayments
    }

    [Fact]
    public void Repayment_with_only_principal_and_interest_omits_zero_components()
    {
        var lines = LoanGlPostingRules.ForRepayment(FullyConfigured, principal: 1000m, interest: 120m, fees: 0m, penalty: 0m, overpayment: 0m);
        AssertBalanced(lines);
        Assert.Equal(3, lines.Count); // fund source debit + portfolio credit + interest credit; fees/penalty/overpayment omitted
    }

    [Fact]
    public void WriteOff_debits_written_off_credits_portfolio_and_receivables()
    {
        var lines = LoanGlPostingRules.ForWriteOff(FullyConfigured, principal: 1000m, interest: 120m, fees: 50m, penalty: 25m);
        AssertBalanced(lines);
        Assert.Equal(1195m, lines.Single(l => l.GlAccountId == 12).Debit);
        Assert.Equal(1000m, lines.Single(l => l.GlAccountId == 2).Credit);
        Assert.Equal(120m, lines.Single(l => l.GlAccountId == 3).Credit);
        Assert.Equal(50m, lines.Single(l => l.GlAccountId == 4).Credit);
        Assert.Equal(25m, lines.Single(l => l.GlAccountId == 5).Credit);
    }

    [Fact]
    public void WriteOffRecovery_debits_fund_source_credits_income_recovery()
    {
        var lines = LoanGlPostingRules.ForWriteOffRecovery(FullyConfigured, 300m);
        AssertBalanced(lines);
        Assert.Equal(300m, lines.Single(l => l.GlAccountId == 1).Debit);
        Assert.Equal(300m, lines.Single(l => l.GlAccountId == 11).Credit);
    }

    [Theory]
    [InlineData(LoanRepaymentComponent.Interest, 8, 3)]
    [InlineData(LoanRepaymentComponent.Fees, 9, 4)]
    [InlineData(LoanRepaymentComponent.Penalty, 10, 5)]
    [InlineData(LoanRepaymentComponent.Principal, 12, 2)]
    public void Waiver_debits_and_credits_the_right_accounts_per_component(LoanRepaymentComponent component, int expectedDebitAccount, int expectedCreditAccount)
    {
        var lines = LoanGlPostingRules.ForWaiver(FullyConfigured, component, 75m);
        AssertBalanced(lines);
        Assert.Equal(75m, lines.Single(l => l.GlAccountId == expectedDebitAccount).Debit);
        Assert.Equal(75m, lines.Single(l => l.GlAccountId == expectedCreditAccount).Credit);
    }

    [Fact]
    public void Waiver_with_zero_amount_produces_no_lines()
    {
        Assert.Empty(LoanGlPostingRules.ForWaiver(FullyConfigured, LoanRepaymentComponent.Interest, 0m));
    }
}
