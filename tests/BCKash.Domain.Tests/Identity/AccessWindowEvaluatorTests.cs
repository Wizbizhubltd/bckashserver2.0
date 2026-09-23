using BCKash.Domain.Identity;
using Xunit;

namespace BCKash.Domain.Tests.Identity;

public class AccessWindowEvaluatorTests
{
    private static User MakeUser(bool timeLimit, string? from = null, string? to = null, string? accessDays = null) => new()
    {
        Email = "test@bckash.test",
        PasswordHash = "hash",
        TimeLimit = timeLimit,
        FromTime = from,
        ToTime = to,
        AccessDays = accessDays,
    };

    [Fact]
    public void No_time_limit_is_always_within_window()
    {
        var user = MakeUser(timeLimit: false);

        Assert.True(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 3, 0, 0)));
    }

    [Fact]
    public void Within_a_same_day_window_is_allowed()
    {
        var user = MakeUser(timeLimit: true, from: "09:00", to: "17:00");

        Assert.True(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 12, 0, 0)));
    }

    [Fact]
    public void Outside_a_same_day_window_is_denied()
    {
        var user = MakeUser(timeLimit: true, from: "09:00", to: "17:00");

        Assert.False(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 20, 0, 0)));
    }

    [Fact]
    public void Overnight_window_wraps_past_midnight()
    {
        var user = MakeUser(timeLimit: true, from: "22:00", to: "06:00");

        Assert.True(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 2, 0, 0)));
        Assert.False(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 12, 0, 0)));
    }

    [Fact]
    public void Restricted_day_blocks_access_even_within_time_window()
    {
        // 2026-01-01 is a Thursday.
        var user = MakeUser(timeLimit: true, from: "00:00", to: "23:59", accessDays: "Monday,Tuesday");

        Assert.False(AccessWindowEvaluator.IsWithinWindow(user, new DateTime(2026, 1, 1, 12, 0, 0)));
    }

    [Fact]
    public void Malformed_time_values_fail_open()
    {
        var user = MakeUser(timeLimit: true, from: "not-a-time", to: "also-not-a-time");

        Assert.True(AccessWindowEvaluator.IsWithinWindow(user, DateTime.Now));
    }
}
