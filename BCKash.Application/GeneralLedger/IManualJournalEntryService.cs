using BCKash.Domain.GeneralLedger;

namespace BCKash.Application.GeneralLedger;

public record ManualJournalEntryLine(int GlAccountId, decimal? Debit, decimal? Credit);

public enum ManualJournalEntryOutcome
{
    Success,
    NotFound,
    TooFewLines,
    LineAmountInvalid,
    Unbalanced,
    AccountNotFound,
    ManualEntriesNotAllowed,
    ClosurePeriod,
    AlreadyApproved,
}

public record ManualJournalEntryWriteResult(
    ManualJournalEntryOutcome Outcome,
    string? Reference = null,
    IReadOnlyList<GlJournalEntry>? Entries = null);

/// <summary>
/// FR-GL-3: manual journal entries, distinct from system-generated postings — every line must
/// target an account with `manual_entries = true`, the whole entry must balance (sum debit ==
/// sum credit), and it starts unapproved (`Approved = false`) until <see cref="ApproveAsync"/>
/// is called; only approved entries count toward reporting (FR-GL-7).
/// </summary>
public interface IManualJournalEntryService
{
    Task<ManualJournalEntryWriteResult> CreateAsync(
        int? officeId, DateOnly date, IReadOnlyList<ManualJournalEntryLine> lines, string narration, CancellationToken cancellationToken = default);

    Task<ManualJournalEntryWriteResult> ApproveAsync(string reference, string? notes, CancellationToken cancellationToken = default);
}
