namespace BCKash.Application.Auth;

/// <summary>Bound from configuration — governs FR-SEC-2 login throttling against the legacy `throttle` table.</summary>
public class LoginThrottleSettings
{
    public const string SectionName = "LoginThrottle";

    /// <summary>Wrong passwords for one account before that account is locked.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Failed sign-ins from one IP address (any account, or emails that don't exist) before that IP
    /// is locked. Kept far above <see cref="MaxFailedAttempts"/> because a whole office — or everyone
    /// behind the server's reverse proxy — can share one address, and a handful of typos by
    /// colleagues must not lock everybody out.
    /// </summary>
    public int IpMaxFailedAttempts { get; set; } = 50;

    public int WindowMinutes { get; set; } = 2;
    public int LockoutMinutes { get; set; } = 2;
}
