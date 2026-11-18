namespace BCKash.Application.Auth;

/// <summary>FR-SEC-2 login throttling, backed by the legacy `throttle` table.</summary>
public interface ILoginThrottleService
{
    /// <summary>
    /// When the account or the IP is locked, the UTC time the lock lifts; otherwise null. The account
    /// locks after <see cref="LoginThrottleSettings.MaxFailedAttempts"/> wrong passwords, the IP after
    /// <see cref="LoginThrottleSettings.IpMaxFailedAttempts"/> failures of any kind.
    /// </summary>
    Task<DateTime?> GetLockedUntilAsync(int? userId, string? ip, CancellationToken cancellationToken = default);

    /// <summary>Only the IP-wide lock — used where the account lock must not apply (e.g. "forgot password").</summary>
    Task<DateTime?> GetIpLockedUntilAsync(string? ip, CancellationToken cancellationToken = default);

    Task RecordFailedAttemptAsync(int? userId, string? ip, CancellationToken cancellationToken = default);

    /// <summary>Forgets an account's failed attempts — after a correct password or a completed password reset.</summary>
    Task ClearFailedAttemptsAsync(int userId, CancellationToken cancellationToken = default);
}
