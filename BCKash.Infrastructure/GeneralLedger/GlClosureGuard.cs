using BCKash.Application.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

public class GlClosureGuard : IGlClosureGuard
{
    private readonly BCKashDbContext _db;

    public GlClosureGuard(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsDatePostableAsync(int? officeId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var isClosed = await _db.GlClosures
            .AnyAsync(c => c.OfficeId == officeId && c.ReopenedAt == null && c.ClosingDate >= date, cancellationToken);

        return !isClosed;
    }
}
