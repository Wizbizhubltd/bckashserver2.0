namespace BCKash.Application.Auth;

/// <summary>Bound from configuration — governs FR-SEC-2 login throttling against the legacy `throttle` table.</summary>
public class LoginThrottleSettings
{
    public const string SectionName = "LoginThrottle";

    public int MaxFailedAttempts { get; set; } = 5;
    public int WindowMinutes { get; set; } = 15;
    public int LockoutMinutes { get; set; } = 15;
}
