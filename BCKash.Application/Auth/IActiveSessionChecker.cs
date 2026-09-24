namespace BCKash.Application.Auth;

/// <summary>
/// Enforces one signed-in device per user: a token is only accepted while its session id is the
/// user's current one. Signing in again (on any device) starts a new session and so invalidates
/// every token issued to the previous one.
/// </summary>
public interface IActiveSessionChecker
{
    Task<bool> IsActiveAsync(int userId, string sessionId, CancellationToken cancellationToken = default);
}
