using System.Globalization;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// When a daily background job next runs: a fixed time of day in Nigeria (WAT, UTC+1 all year — no
/// daylight saving). Pinning the jobs to the clock, instead of "on start-up, then every 24 hours",
/// keeps their bulk work over the loan tables out of office hours no matter when the API was deployed.
/// </summary>
public static class DailyRunSchedule
{
    private static readonly TimeSpan Wat = TimeSpan.FromHours(1);

    /// <summary>"HH:mm" from configuration, or <paramref name="fallback"/> when unset or unreadable.</summary>
    public static TimeOnly Parse(string? configured, TimeOnly fallback) =>
        TimeOnly.TryParseExact(configured, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : fallback;

    /// <summary>How long from <paramref name="nowUtc"/> until the next <paramref name="watTime"/>.</summary>
    public static TimeSpan Until(TimeOnly watTime, DateTime nowUtc)
    {
        var nowWat = nowUtc + Wat;
        var next = nowWat.Date + watTime.ToTimeSpan();
        if (next <= nowWat)
        {
            next = next.AddDays(1);
        }

        return next - nowWat;
    }
}
