using BCKash.Domain.Organization;

namespace BCKash.Domain.Loans;

/// <summary>A penalty charge as it applies to one loan — <see cref="GraceDays"/> already resolved against the overdue rules.</summary>
public record PenaltyRule(
    int ChargeId, ChargeOption Option, decimal Amount, decimal? MinimumAmount, decimal? MaximumAmount,
    int GraceDays, int? RepeatEveryDays, decimal? MaxTotalPercent);

/// <summary>One charge of a penalty: the <see cref="Occurrence"/>-th time it falls due, on <see cref="Date"/>.</summary>
public record DuePenalty(int Occurrence, DateOnly Date, decimal Amount);

/// <summary>
/// Works out which occurrences of a late-repayment or default penalty are due, and for how much.
/// Pure — the caller supplies what's already been charged and the balance the percentage is taken of.
/// </summary>
public static class LoanPenaltyCalculator
{
    /// <summary>
    /// Occurrences that fell due more than this many days ago are never charged. That lets a missed
    /// daily run catch up, without charging borrowers retroactively for lateness from before the
    /// penalty or automatic charging existed (legacy arrears would otherwise be hit with years of
    /// backdated penalties the moment it's switched on).
    /// </summary>
    public const int CatchUpDays = 7;

    /// <param name="triggerDate">The instalment's due date (late fee) or the loan's final repayment date (default penalty).</param>
    /// <param name="applied">Occurrences already charged, and their total.</param>
    /// <param name="baseAmount">What a percentage is taken of — already chosen for <see cref="PenaltyRule.Option"/>.</param>
    /// <param name="disbursedAmount">What <see cref="PenaltyRule.MaxTotalPercent"/> is a percentage of.</param>
    public static IReadOnlyList<DuePenalty> Due(
        PenaltyRule rule, DateOnly triggerDate, DateOnly today,
        IReadOnlySet<int> applied, decimal appliedTotal, decimal baseAmount, decimal disbursedAmount)
    {
        // Charged the day after the grace period ends.
        var first = triggerDate.AddDays(rule.GraceDays + 1);
        if (today < first)
        {
            return [];
        }

        var occurrencesDue = rule.RepeatEveryDays is { } every ? 1 + (today.DayNumber - first.DayNumber) / every : 1;
        var cap = rule.MaxTotalPercent is { } pct ? Math.Round(disbursedAmount * pct / 100m, 2) : (decimal?)null;
        var oldestChargeable = today.AddDays(-CatchUpDays);

        var due = new List<DuePenalty>();
        var total = appliedTotal;
        for (var occurrence = 1; occurrence <= occurrencesDue; occurrence++)
        {
            var date = rule.RepeatEveryDays is { } n ? first.AddDays((occurrence - 1) * n) : first;
            if (applied.Contains(occurrence) || date <= oldestChargeable)
            {
                continue;
            }

            var amount = AmountFor(rule, baseAmount);
            if (cap is { } limit)
            {
                amount = Math.Min(amount, limit - total);
            }

            if (amount <= 0)
            {
                break; // cap reached, or nothing left to take a percentage of
            }

            total += amount;
            due.Add(new DuePenalty(occurrence, date, amount));
        }

        return due;
    }

    public static decimal AmountFor(PenaltyRule rule, decimal baseAmount)
    {
        if (rule.Option == ChargeOption.Flat)
        {
            return rule.Amount;
        }

        var amount = Math.Round(Math.Max(baseAmount, 0) * rule.Amount / 100m, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
        {
            return 0; // nothing owed on the base — no minimum charge either
        }

        if (rule.MinimumAmount is { } min) amount = Math.Max(amount, min);
        if (rule.MaximumAmount is { } max) amount = Math.Min(amount, max);
        return amount;
    }
}
