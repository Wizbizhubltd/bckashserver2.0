using BCKash.Domain.Expenses;
using Xunit;

namespace BCKash.Domain.Tests.Expenses;

/// <summary>Recurring expenses' "advance to next occurrence" logic — Phase 8's second acceptance criterion.</summary>
public class ExpenseRecurrenceRulesTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    [Theory]
    [InlineData(ExpenseRecurType.Day, "1", 2026, 1, 16)]
    [InlineData(ExpenseRecurType.Week, "1", 2026, 1, 22)]
    [InlineData(ExpenseRecurType.Month, "3", 2026, 4, 15)]
    [InlineData(ExpenseRecurType.Year, "1", 2027, 1, 15)]
    public void Advances_by_the_configured_interval_and_unit(ExpenseRecurType type, string frequency, int year, int month, int day)
    {
        var next = ExpenseRecurrenceRules.NextDate(Start, type, frequency);
        Assert.Equal(new DateOnly(year, month, day), next);
    }

    [Fact]
    public void Unparseable_frequency_defaults_to_an_interval_of_one()
    {
        var next = ExpenseRecurrenceRules.NextDate(Start, ExpenseRecurType.Month, "not-a-number");
        Assert.Equal(new DateOnly(2026, 2, 15), next);
    }
}
