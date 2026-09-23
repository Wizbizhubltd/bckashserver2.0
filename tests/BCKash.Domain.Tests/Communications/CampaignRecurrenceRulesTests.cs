using BCKash.Domain.Communications;
using Xunit;

namespace BCKash.Domain.Tests.Communications;

public class CampaignRecurrenceRulesTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    [Theory]
    [InlineData(CampaignRecurFrequency.Days, "1", 2026, 1, 16)]
    [InlineData(CampaignRecurFrequency.Weeks, "2", 2026, 1, 29)]
    [InlineData(CampaignRecurFrequency.Months, "1", 2026, 2, 15)]
    [InlineData(CampaignRecurFrequency.Years, "1", 2027, 1, 15)]
    public void Advances_by_the_configured_interval_and_unit(CampaignRecurFrequency freq, string interval, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), CampaignRecurrenceRules.NextDate(Start, freq, interval));
    }

    [Fact]
    public void Unparseable_interval_defaults_to_one()
    {
        Assert.Equal(new DateOnly(2026, 2, 15), CampaignRecurrenceRules.NextDate(Start, CampaignRecurFrequency.Months, "not-a-number"));
    }
}
