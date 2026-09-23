using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Domain.Tests.Savings;

/// <summary>FR-SAV-3's balance-floor enforcement — the direct target of the phase's acceptance criterion 1.</summary>
public class SavingsBalanceRulesTests
{
    [Fact]
    public void Without_overdraft_the_floor_is_the_minimum_balance()
    {
        Assert.Equal(500m, SavingsBalanceRules.EffectiveFloor(allowOverdraft: false, minimumBalance: 500m, overdraftLimit: 1000m));
    }

    [Fact]
    public void Without_overdraft_a_null_minimum_balance_floors_at_zero()
    {
        Assert.Equal(0m, SavingsBalanceRules.EffectiveFloor(allowOverdraft: false, minimumBalance: null, overdraftLimit: null));
    }

    [Fact]
    public void With_overdraft_the_floor_is_the_negative_overdraft_limit()
    {
        Assert.Equal(-1000m, SavingsBalanceRules.EffectiveFloor(allowOverdraft: true, minimumBalance: 500m, overdraftLimit: 1000m));
    }

    [Fact]
    public void Withdrawal_below_minimum_balance_is_rejected()
    {
        var floor = SavingsBalanceRules.EffectiveFloor(allowOverdraft: false, minimumBalance: 500m, overdraftLimit: null);
        Assert.False(SavingsBalanceRules.CanWithdraw(currentBalance: 1000m, amount: 600m, floor)); // would land at 400, below the 500 floor
    }

    [Fact]
    public void Withdrawal_that_lands_exactly_on_the_floor_is_allowed()
    {
        var floor = SavingsBalanceRules.EffectiveFloor(allowOverdraft: false, minimumBalance: 500m, overdraftLimit: null);
        Assert.True(SavingsBalanceRules.CanWithdraw(currentBalance: 1000m, amount: 500m, floor));
    }

    [Fact]
    public void Withdrawal_beyond_minimum_balance_succeeds_when_overdraft_covers_it()
    {
        var floor = SavingsBalanceRules.EffectiveFloor(allowOverdraft: true, minimumBalance: 500m, overdraftLimit: 1000m);
        Assert.True(SavingsBalanceRules.CanWithdraw(currentBalance: 200m, amount: 900m, floor)); // lands at -700, within the -1000 overdraft floor
    }

    [Fact]
    public void Withdrawal_beyond_the_overdraft_limit_is_rejected()
    {
        var floor = SavingsBalanceRules.EffectiveFloor(allowOverdraft: true, minimumBalance: 500m, overdraftLimit: 1000m);
        Assert.False(SavingsBalanceRules.CanWithdraw(currentBalance: 200m, amount: 1300m, floor)); // would land at -1100, beyond -1000
    }
}
