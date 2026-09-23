namespace BCKash.Application.Auth;

/// <summary>
/// Computes a user's effective permission set (FR-SEC-4): role permissions ∪ direct
/// per-user overrides. Backs both JWT claim population at login and the
/// policy-based authorization handler on every subsequent request.
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlyCollection<string>> GetEffectivePermissionSlugsAsync(int userId, CancellationToken cancellationToken = default);
}
