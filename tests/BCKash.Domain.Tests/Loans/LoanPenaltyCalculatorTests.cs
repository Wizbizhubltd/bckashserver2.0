using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Domain.Tests.Loans;

public class LoanPenaltyCalculatorTests
{
    private static readonly DateOnly Due = new(2026, 9, 1);

    private static PenaltyRule Rule(ChargeOption option = ChargeOption.InstallmentPrincipalInterestDue, decimal amount = 5, int grace = 3, int? repeat = null, decimal? cap = null, decimal? min = null, decimal? max = null) =>
        new(1, option, amount, min, max, grace, repeat, cap);

    private static IReadOnlyList<DuePenalty> Run(PenaltyRule rule, DateOnly today, ISet<int>? applied = null, decimal appliedTotal = 0, decimal baseAmount = 1100, decimal disbursed = 10000) =>
        LoanPenaltyCalculator.Due(rule, Due, today, (IReadOnlySet<int>)(applied ?? new HashSet<int>()), appliedTotal, baseAmount, disbursed);

    [Fact]
    public void Nothing_is_charged_inside_the_grace_period() =>
        Assert.Empty(Run(Rule(grace: 3), Due.AddDays(3)));

    [Fact]
    public void The_penalty_is_charged_the_day_after_grace_ends_on_the_missed_instalment()
    {
        var due = Assert.Single(Run(Rule(grace: 3), Due.AddDays(4)));
        Assert.Equal(55m, due.Amount); // 5% of 1,100
        Assert.Equal(Due.AddDays(4), due.Date);
    }

    [Fact]
    public void A_one_off_penalty_is_never_charged_twice() =>
        Assert.Empty(Run(Rule(), Due.AddDays(5), applied: new HashSet<int> { 1 }, appliedTotal: 55));

    [Fact]
    public void A_repeating_penalty_stops_at_its_cap()
    {
        // Cap 1% of 10,000 = 100: 55 already charged, so the next occurrence is only 45, then nothing.
        var rule = Rule(repeat: 30, cap: 1);
        var second = Assert.Single(Run(rule, Due.AddDays(34), applied: new HashSet<int> { 1 }, appliedTotal: 55));
        Assert.Equal(45m, second.Amount);
        Assert.Empty(Run(rule, Due.AddDays(64), applied: new HashSet<int> { 1, 2 }, appliedTotal: 100));
    }

    [Fact]
    public void Old_arrears_are_not_charged_retroactively()
    {
        var today = Due.AddDays(400);
        Assert.Empty(Run(Rule(), today)); // one-off, fell due over a year ago

        // Repeating: only an occurrence falling due in the last week is charged.
        var charged = Run(Rule(grace: 0, repeat: 30), today);
        Assert.All(charged, c => Assert.True(c.Date > today.AddDays(-LoanPenaltyCalculator.CatchUpDays)));
        Assert.True(charged.Count <= 1);
    }

    [Fact]
    public void A_missed_run_is_caught_up_within_a_week() =>
        Assert.Single(Run(Rule(grace: 3), Due.AddDays(9)));

    [Theory]
    [InlineData(1100, 20, 100, 55)]  // within bounds
    [InlineData(100, 20, 100, 20)]   // raised to the minimum
    [InlineData(5000, 20, 100, 100)] // lowered to the maximum
    [InlineData(0, 20, 100, 0)]      // nothing owed — no minimum charge
    public void Percentage_amounts_respect_minimum_and_maximum(decimal baseAmount, decimal min, decimal max, decimal expected) =>
        Assert.Equal(expected, LoanPenaltyCalculator.AmountFor(Rule(min: min, max: max), baseAmount));
}
