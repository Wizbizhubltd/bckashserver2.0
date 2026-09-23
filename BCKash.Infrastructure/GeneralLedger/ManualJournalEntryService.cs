using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.GeneralLedger;

public class ManualJournalEntryService : IManualJournalEntryService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IGlClosureGuard _closureGuard;

    public ManualJournalEntryService(BCKashDbContext db, ICurrentUserContext currentUser, IGlClosureGuard closureGuard)
    {
        _db = db;
        _currentUser = currentUser;
        _closureGuard = closureGuard;
    }

    public async Task<ManualJournalEntryWriteResult> CreateAsync(
        int? officeId, DateOnly date, IReadOnlyList<ManualJournalEntryLine> lines, string narration, CancellationToken cancellationToken = default)
    {
        if (lines.Count < 2)
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.TooFewLines);
        }

        decimal totalDebit = 0, totalCredit = 0;
        foreach (var line in lines)
        {
            var hasDebit = line.Debit is > 0;
            var hasCredit = line.Credit is > 0;
            if (hasDebit == hasCredit)
            {
                return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.LineAmountInvalid);
            }

            totalDebit += line.Debit ?? 0m;
            totalCredit += line.Credit ?? 0m;
        }

        if (totalDebit != totalCredit)
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.Unbalanced);
        }

        if (!await _closureGuard.IsDatePostableAsync(officeId, date, cancellationToken))
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.ClosurePeriod);
        }

        var accountIds = lines.Select(l => l.GlAccountId).Distinct().ToList();
        var accounts = await _db.GlAccounts.Where(a => accountIds.Contains(a.Id)).ToListAsync(cancellationToken);
        if (accounts.Count != accountIds.Count)
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.AccountNotFound);
        }

        if (accounts.Any(a => !a.ManualEntries))
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.ManualEntriesNotAllowed);
        }

        var reference = $"MANUAL-{Guid.NewGuid():N}";
        var entries = new List<GlJournalEntry>();
        foreach (var line in lines)
        {
            var entry = new GlJournalEntry
            {
                OfficeId = officeId,
                GlAccountId = line.GlAccountId,
                TransactionType = GlTransactionType.ManualEntry,
                Debit = line.Debit,
                Credit = line.Credit,
                Reference = reference,
                Date = date,
                Narration = narration,
                ManualEntry = true,
                Approved = false,
                CreatedById = _currentUser.UserId,
            };
            entries.Add(entry);
            _db.GlJournalEntries.Add(entry);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.Success, reference, entries);
    }

    public async Task<ManualJournalEntryWriteResult> ApproveAsync(string reference, string? notes, CancellationToken cancellationToken = default)
    {
        var entries = await _db.GlJournalEntries.Where(e => e.Reference == reference).ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.NotFound);
        }

        if (entries.Any(e => e.Approved))
        {
            return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.AlreadyApproved);
        }

        var approvedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var entry in entries)
        {
            entry.Approved = true;
            entry.ApprovedById = _currentUser.UserId;
            entry.ApprovedDate = approvedDate;
            entry.ApprovedNotes = notes;
            entry.ModifiedById = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ManualJournalEntryWriteResult(ManualJournalEntryOutcome.Success, reference, entries);
    }
}
