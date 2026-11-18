using BCKash.Infrastructure.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Jobs;

public class DailyRunScheduleTests
{
    [Fact]
    public void Runs_at_the_next_occurrence_of_the_nigerian_time()
    {
        var oneAm = new TimeOnly(1, 0);

        // 10:00 UTC is 11:00 WAT — next 01:00 WAT is 14 hours away.
        Assert.Equal(TimeSpan.FromHours(14), DailyRunSchedule.Until(oneAm, new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc)));
        // 23:30 UTC is 00:30 WAT — 01:00 WAT is half an hour away, the same (Nigerian) night.
        Assert.Equal(TimeSpan.FromMinutes(30), DailyRunSchedule.Until(oneAm, new DateTime(2026, 11, 18, 23, 30, 0, DateTimeKind.Utc)));
        // Exactly at the time: the next run is tomorrow, not now again.
        Assert.Equal(TimeSpan.FromHours(24), DailyRunSchedule.Until(oneAm, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Configured_time_falls_back_when_missing_or_malformed()
    {
        Assert.Equal(new TimeOnly(7, 30), DailyRunSchedule.Parse("07:30", new TimeOnly(1, 0)));
        Assert.Equal(new TimeOnly(1, 0), DailyRunSchedule.Parse(null, new TimeOnly(1, 0)));
        Assert.Equal(new TimeOnly(1, 0), DailyRunSchedule.Parse("7am", new TimeOnly(1, 0)));
    }
}
