using BCKash.Application.GeneralLedger;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

public class GlJournalEntryService : IGlJournalEntryService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public GlJournalEntryService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GlJournalEntryReversalResult> ReverseByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var entries = await _db.GlJournalEntries.Where(e => e.Reference == reference).ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return new GlJournalEntryReversalResult(GlJournalEntryReversalOutcome.NotFound);
        }

        if (entries.All(e => e.Reversed))
        {
            return new GlJournalEntryReversalResult(GlJournalEntryReversalOutcome.AlreadyReversed);
        }

        foreach (var entry in entries.Where(e => !e.Reversed))
        {
            entry.Reversed = true;
            entry.ModifiedById = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new GlJournalEntryReversalResult(GlJournalEntryReversalOutcome.Success, entries);
    }
}
