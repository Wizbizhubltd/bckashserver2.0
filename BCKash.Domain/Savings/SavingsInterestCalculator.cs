namespace BCKash.Domain.Savings;

/// <summary>
/// FR-SAV-4's interest calculation, both types the schema names. Pure — no I/O; the
/// infrastructure layer is responsible for building the daily-balance list by replaying an
/// account's transactions. <c>annualRatePercent</c> follows the same convention as
/// <c>LoanScheduleGenerator</c> — a plain percentage (5 means 5%), not a fraction.
///
/// Both methods agree exactly when the balance is constant across the whole period (a genuine,
/// testable equivalence property, not a shortcut — see docs/savings-interest-spec.md) and
/// genuinely differ whenever the balance changes mid-period, since "average balance" here uses
/// the standard two-point (opening/closing) simplification rather than a full time-weighted
/// average.
/// </summary>
public static class SavingsInterestCalculator
{
    public static decimal CalculateDailyBalance(IReadOnlyList<(DateOnly Date, decimal Balance)> dailyBalances, decimal annualRatePercent, int yearDays)
    {
        if (dailyBalances.Count == 0 || yearDays <= 0)
        {
            return 0m;
        }

        var dailyRate = (annualRatePercent / 100m) / yearDays;
        return dailyBalances.Sum(d => d.Balance * dailyRate);
    }

    public static decimal CalculateAverageBalance(decimal openingBalance, decimal closingBalance, int daysInPeriod, decimal annualRatePercent, int yearDays)
    {
        if (daysInPeriod <= 0 || yearDays <= 0)
        {
            return 0m;
        }

        var averageBalance = (openingBalance + closingBalance) / 2m;
        var dailyRate = (annualRatePercent / 100m) / yearDays;
        return averageBalance * dailyRate * daysInPeriod;
    }
}
