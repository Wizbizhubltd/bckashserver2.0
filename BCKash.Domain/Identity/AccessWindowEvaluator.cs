namespace BCKash.Domain.Identity;

/// <summary>
/// Evaluates the legacy `time_limit`/`from_time`/`to_time`/`access_days` columns
/// (BR-SEC-4) at login. Role-level windows and per-request enforcement are FRD §1.5's
/// longer-term target — Phase 0 only wires the user-level check at login, per the
/// Phase 0 acceptance criteria.
/// </summary>
public static class AccessWindowEvaluator
{
    public static bool IsWithinWindow(User user, DateTime now)
    {
        if (!user.TimeLimit)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(user.AccessDays) && !IsAllowedDay(user.AccessDays, now.DayOfWeek))
        {
            return false;
        }

        if (!TimeOnly.TryParse(user.FromTime, out var from) || !TimeOnly.TryParse(user.ToTime, out var to))
        {
            // Can't parse the configured window — fail open rather than lock everyone out
            // over a malformed legacy value; this is exactly the kind of row a real data
            // copy would let us tighten once we can see the actual stored format.
            return true;
        }

        var current = TimeOnly.FromDateTime(now);
        return from <= to ? current >= from && current <= to : current >= from || current <= to;
    }

    private static bool IsAllowedDay(string accessDays, DayOfWeek day)
    {
        var tokens = accessDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (int.TryParse(token, out var numericDay) && numericDay == (int)day)
            {
                return true;
            }

            if (Enum.TryParse<DayOfWeek>(token, ignoreCase: true, out var namedDay) && namedDay == day)
            {
                return true;
            }
        }

        return false;
    }
}
