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

    public async Task<bool> IsLockedOutAsync(int? userId, string? ip, CancellationToken cancellationToken = default)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-_settings.WindowMinutes);

        var recentAttempts = await _db.Throttles
            .Where(t => t.Type == LoginThrottleType && t.CreatedAt >= windowStart)
            .Where(t => (userId.HasValue && t.UserId == userId) || (ip != null && t.Ip == ip))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        if (recentAttempts.Count < _settings.MaxFailedAttempts)
        {
            return false;
        }

        // Locked for LockoutMinutes measured from the most recent failure (not the window edge),
        // so continuing to retry during the cooldown pushes the unlock time back out — by design,
        // since otherwise a fixed-from-Nth-failure lockout would let an attacker keep guessing at
        // the same steady rate right through it.
        var mostRecentFailure = recentAttempts[0]!.Value;
        return mostRecentFailure.AddMinutes(_settings.LockoutMinutes) > DateTime.UtcNow;
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
}
