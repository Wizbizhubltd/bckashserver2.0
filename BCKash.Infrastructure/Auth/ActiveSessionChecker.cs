using BCKash.Application.Auth;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Auth;

// Runs on every authenticated request. The query goes through the EF second-level cache, which is
// invalidated whenever the users table is written — so a new sign-in (which writes
// users.active_session_id) takes effect on the old device's very next request.
public class ActiveSessionChecker : IActiveSessionChecker
{
    private readonly BCKashDbContext _db;

    public ActiveSessionChecker(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsActiveAsync(int userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var activeSessionId = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ActiveSessionId)
            .FirstOrDefaultAsync(cancellationToken);

        return activeSessionId is not null && activeSessionId == sessionId;
    }
}
