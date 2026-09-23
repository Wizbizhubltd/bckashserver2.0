using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Domain.Tests.Savings;

/// <summary>
/// FR-SAV-4's two calculation types — see docs/savings-interest-spec.md for the worked
/// examples this mirrors. 7.2%/year on a 360-day convention gives a clean daily rate
/// (0.0002/day) so every expected figure below is exact, hand-derivable arithmetic, not a
/// repeating decimal. Constant-balance case: the two methods must agree exactly (a genuine
/// property, not a coincidence). Mid-period-deposit case: they genuinely differ, since
/// "average balance" here is the standard two-point (opening/closing) simplification, not a
/// full time-weighted average.
/// </summary>
public class SavingsInterestCalculatorTests
{
    private const decimal AnnualRate = 7.2m; // 7.2% / 360 days = 0.0002/day exactly.
    private const int YearDays = 360;

    [Fact]
    public void Daily_and_average_balance_agree_exactly_when_the_balance_is_constant()
    {
        // 10,000 held for 30 days.
        var dailyBalances = Enumerable.Range(1, 30)
            .Select(day => (new DateOnly(2026, 1, day), 10000m))
            .ToList();

        var daily = SavingsInterestCalculator.CalculateDailyBalance(dailyBalances, AnnualRate, YearDays);
        var average = SavingsInterestCalculator.CalculateAverageBalance(openingBalance: 10000m, closingBalance: 10000m, daysInPeriod: 30, AnnualRate, YearDays);

        Assert.Equal(daily, average);
        Assert.Equal(60.00m, daily); // 10000 * 0.0002 * 30
    }

    [Fact]
    public void Daily_and_average_balance_genuinely_differ_when_a_deposit_lands_mid_period()
    {
        // 10,000 held for the first 10 days, then a 5,000 deposit lands, holding 15,000 for the remaining 20 days.
        var dailyBalances = Enumerable.Range(1, 10).Select(day => (new DateOnly(2026, 1, day), 10000m))
            .Concat(Enumerable.Range(11, 20).Select(day => (new DateOnly(2026, 1, day), 15000m)))
            .ToList();

        var daily = SavingsInterestCalculator.CalculateDailyBalance(dailyBalances, AnnualRate, YearDays);
        Assert.Equal(80.00m, daily); // (10*10000 + 20*15000) * 0.0002 = 400000 * 0.0002

        var average = SavingsInterestCalculator.CalculateAverageBalance(openingBalance: 10000m, closingBalance: 15000m, daysInPeriod: 30, AnnualRate, YearDays);
        Assert.Equal(75.00m, average); // ((10000+15000)/2) * 0.0002 * 30 = 12500 * 0.0002 * 30

        Assert.NotEqual(daily, average);
    }

    [Fact]
    public void Empty_period_produces_zero_interest()
    {
        Assert.Equal(0m, SavingsInterestCalculator.CalculateDailyBalance([], AnnualRate, YearDays));
        Assert.Equal(0m, SavingsInterestCalculator.CalculateAverageBalance(1000m, 1000m, 0, AnnualRate, YearDays));
    }
}
