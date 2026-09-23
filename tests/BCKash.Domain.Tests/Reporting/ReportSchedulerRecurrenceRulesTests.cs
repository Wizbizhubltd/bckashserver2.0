using BCKash.Domain.Reporting;
using Xunit;

namespace BCKash.Domain.Tests.Reporting;

public class ReportSchedulerRecurrenceRulesTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    [Theory]
    [InlineData(ReportSchedulerRecurFrequency.Daily, "1", 2026, 1, 16)]
    [InlineData(ReportSchedulerRecurFrequency.Weekly, "1", 2026, 1, 22)]
    [InlineData(ReportSchedulerRecurFrequency.Monthly, "3", 2026, 4, 15)]
    [InlineData(ReportSchedulerRecurFrequency.Yearly, "1", 2027, 1, 15)]
    public void Advances_by_the_configured_interval_and_unit(ReportSchedulerRecurFrequency freq, string interval, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), ReportSchedulerRecurrenceRules.NextDate(Start, freq, interval));
    }
}
