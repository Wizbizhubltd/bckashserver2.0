using BCKash.Domain.Payroll;
using Xunit;

namespace BCKash.Domain.Tests.Payroll;

/// <summary>Recurring payroll's "advance to next occurrence" logic — Phase 8's second acceptance criterion.</summary>
public class PayrollRecurrenceRulesTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    [Theory]
    [InlineData(PayrollRecurType.Days, "1", 2026, 1, 16)]
    [InlineData(PayrollRecurType.Weeks, "1", 2026, 1, 22)]
    [InlineData(PayrollRecurType.Weeks, "2", 2026, 1, 29)]
    [InlineData(PayrollRecurType.Months, "1", 2026, 2, 15)]
    [InlineData(PayrollRecurType.Years, "1", 2027, 1, 15)]
    public void Advances_by_the_configured_interval_and_unit(PayrollRecurType type, string frequency, int year, int month, int day)
    {
        var next = PayrollRecurrenceRules.NextDate(Start, type, frequency);
        Assert.Equal(new DateOnly(year, month, day), next);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-3")]
    public void Unparseable_or_non_positive_frequency_defaults_to_an_interval_of_one(string? frequency)
    {
        var next = PayrollRecurrenceRules.NextDate(Start, PayrollRecurType.Months, frequency);
        Assert.Equal(new DateOnly(2026, 2, 15), next);
    }
}
