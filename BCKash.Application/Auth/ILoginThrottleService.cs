namespace BCKash.Application.Auth;

/// <summary>FR-SEC-2 login throttling, backed by the legacy `throttle` table.</summary>
public interface ILoginThrottleService
{
    /// <summary>True when the user/IP has hit <see cref="LoginThrottleSettings.MaxFailedAttempts"/> within the rolling window.</summary>
    Task<bool> IsLockedOutAsync(int? userId, string? ip, CancellationToken cancellationToken = default);

    Task RecordFailedAttemptAsync(int? userId, string? ip, CancellationToken cancellationToken = default);
}
