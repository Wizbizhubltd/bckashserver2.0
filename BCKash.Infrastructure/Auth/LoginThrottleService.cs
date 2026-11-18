using BCKash.Application.Auth;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Auth;

public class LoginThrottleService : ILoginThrottleService
{
    private const string LoginThrottleType = "login";

    private readonly BCKashDbContext _db;
    private readonly LoginThrottleSettings _settings;

    public LoginThrottleService(BCKashDbContext db, IOptions<LoginThrottleSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<DateTime?> GetLockedUntilAsync(int? userId, string? ip, CancellationToken cancellationToken = default)
    {
        if (!await LockoutEnabledAsync(cancellationToken))
        {
            return null;
        }

        // The account and the IP are counted separately, each against its own limit. Counting them
        // together (account OR IP against one small limit) let a few typos by anyone sharing the
        // office's or proxy's IP lock every account behind it — and keep re-locking it.
        var accountLock = userId.HasValue
            ? await LockedUntilAsync(t => t.UserId == userId, _settings.MaxFailedAttempts, cancellationToken)
            : null;
        var ipLock = ip is null ? null : await IpLockedUntilAsync(ip, cancellationToken);

        return Later(accountLock, ipLock);
    }

    public async Task<DateTime?> GetIpLockedUntilAsync(string? ip, CancellationToken cancellationToken = default) =>
        ip is null || !await LockoutEnabledAsync(cancellationToken)
            ? null
            : await IpLockedUntilAsync(ip, cancellationToken);

    private Task<DateTime?> IpLockedUntilAsync(string ip, CancellationToken cancellationToken) =>
        LockedUntilAsync(t => t.Ip == ip, _settings.IpMaxFailedAttempts, cancellationToken);

    /// <summary>Settings → Roles &amp; Access → "Lock accounts after failed sign-ins" (see <see cref="LoginLockoutRules"/>).</summary>
    private async Task<bool> LockoutEnabledAsync(CancellationToken cancellationToken)
    {
        var value = await _db.Settings
            .Where(s => s.SettingKey == LoginLockoutRules.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return LoginLockoutRules.IsEnabled(value);
    }

    public async Task RecordFailedAttemptAsync(int? userId, string? ip, CancellationToken cancellationToken = default)
    {
        _db.Throttles.Add(new Throttle
        {
            Type = LoginThrottleType,
            UserId = userId,
            Ip = ip,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task ClearFailedAttemptsAsync(int userId, CancellationToken cancellationToken = default) =>
        _db.Throttles
            .Where(t => t.Type == LoginThrottleType && t.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    private async Task<DateTime?> LockedUntilAsync(
        System.Linq.Expressions.Expression<Func<Throttle, bool>> scope, int limit, CancellationToken cancellationToken)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-_settings.WindowMinutes);

        var recentAttempts = await _db.Throttles
            .Where(t => t.Type == LoginThrottleType && t.CreatedAt >= windowStart)
            .Where(scope)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (recentAttempts.Count < limit)
        {
            return null;
        }

        // Locked for LockoutMinutes measured from the most recent failure (not the window edge).
        // Attempts made while locked are rejected before they're recorded, so waiting it out
        // always works.
        var lockedUntil = recentAttempts[0]!.Value.AddMinutes(_settings.LockoutMinutes);
        return lockedUntil > DateTime.UtcNow ? lockedUntil : null;
    }

    private static DateTime? Later(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);
}
