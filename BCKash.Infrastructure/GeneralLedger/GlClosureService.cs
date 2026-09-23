using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

public class GlClosureService : IGlClosureService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public GlClosureService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GlClosureWriteResult> CloseAsync(int? officeId, DateOnly closingDate, string? notes, CancellationToken cancellationToken = default)
    {
        var alreadyClosed = await _db.GlClosures
            .AnyAsync(c => c.OfficeId == officeId && c.ReopenedAt == null && c.ClosingDate >= closingDate, cancellationToken);
        if (alreadyClosed)
        {
            return new GlClosureWriteResult(GlClosureWriteOutcome.AlreadyClosed);
        }

        var closure = new GlClosure
        {
            OfficeId = officeId,
            ClosingDate = closingDate,
            Notes = notes,
            CreatedById = _currentUser.UserId,
        };
        _db.GlClosures.Add(closure);
        await _db.SaveChangesAsync(cancellationToken);
        return new GlClosureWriteResult(GlClosureWriteOutcome.Success, closure);
    }

    public async Task<GlClosureWriteResult> ReopenAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        var closure = await _db.GlClosures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (closure is null)
        {
            return new GlClosureWriteResult(GlClosureWriteOutcome.NotFound);
        }

        if (closure.ReopenedAt.HasValue)
        {
            return new GlClosureWriteResult(GlClosureWriteOutcome.AlreadyReopened);
        }

        closure.ReopenedAt = DateTime.UtcNow;
        closure.ReopenedById = _currentUser.UserId;
        closure.ModifiedById = _currentUser.UserId;
        closure.Notes = notes is null ? closure.Notes : $"{closure.Notes}\n[Reopened] {notes}";

        await _db.SaveChangesAsync(cancellationToken);
        return new GlClosureWriteResult(GlClosureWriteOutcome.Success, closure);
    }
}
