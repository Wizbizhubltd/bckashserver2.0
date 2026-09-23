using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Domain.Tests.GeneralLedger;

/// <summary>Every SavingsGlPostingRules method must return balanced lines or an empty list when the product isn't fully configured — see docs/savings-interest-spec.md.</summary>
public class SavingsGlPostingRulesTests
{
    private static readonly SavingsProduct FullyConfigured = new()
    {
        GlAccountSavingsReferenceId = 1,
        GlAccountOverdraftPortfolioId = 2,
        GlAccountSavingsControlId = 3,
        GlAccountInterestOnSavingsId = 4,
        GlAccountSavingsWrittenOffId = 5,
        GlAccountIncomeInterestId = 6,
        GlAccountIncomeFeeId = 7,
        GlAccountIncomePenaltyId = 8,
    };

    private static void AssertBalanced(IReadOnlyList<GlPostingLine> lines)
    {
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Sum(l => l.Debit ?? 0m), lines.Sum(l => l.Credit ?? 0m));
    }

    [Fact]
    public void Deposit_debits_reference_credits_control()
    {
        var lines = SavingsGlPostingRules.ForDeposit(FullyConfigured, 500m);
        AssertBalanced(lines);
        Assert.Equal(500m, lines.Single(l => l.GlAccountId == 1).Debit);
        Assert.Equal(500m, lines.Single(l => l.GlAccountId == 3).Credit);
    }

    [Fact]
    public void Withdrawal_debits_control_credits_reference()
    {
        var lines = SavingsGlPostingRules.ForWithdrawal(FullyConfigured, 200m);
        AssertBalanced(lines);
        Assert.Equal(200m, lines.Single(l => l.GlAccountId == 3).Debit);
        Assert.Equal(200m, lines.Single(l => l.GlAccountId == 1).Credit);
    }

    [Fact]
    public void Interest_debits_expense_credits_control()
    {
        var lines = SavingsGlPostingRules.ForInterest(FullyConfigured, 15m);
        AssertBalanced(lines);
        Assert.Equal(15m, lines.Single(l => l.GlAccountId == 4).Debit);
        Assert.Equal(15m, lines.Single(l => l.GlAccountId == 3).Credit);
    }

    [Theory]
    [InlineData(false, 7)] // fee -> income fee
    [InlineData(true, 8)] // penalty -> income penalty
    public void Charge_debits_control_credits_the_right_income_account(bool isPenalty, int expectedIncomeAccount)
    {
        var lines = SavingsGlPostingRules.ForCharge(FullyConfigured, isPenalty, 50m);
        AssertBalanced(lines);
        Assert.Equal(50m, lines.Single(l => l.GlAccountId == 3).Debit);
        Assert.Equal(50m, lines.Single(l => l.GlAccountId == expectedIncomeAccount).Credit);
    }

    [Fact]
    public void Unmapped_account_produces_no_lines()
    {
        var product = new SavingsProduct { GlAccountSavingsControlId = 3 }; // reference missing
        Assert.Empty(SavingsGlPostingRules.ForDeposit(product, 100m));
    }
}
