using BCKash.Domain.GeneralLedger;

namespace BCKash.Application.GeneralLedger;

public enum GlJournalEntryReversalOutcome
{
    Success,
    NotFound,
    AlreadyReversed,
}

public record GlJournalEntryReversalResult(GlJournalEntryReversalOutcome Outcome, IReadOnlyList<GlJournalEntry>? Entries = null);

/// <summary>
/// FR-GL-6: journal entries are reversed by flag, never hard-deleted. Reversal always acts on
/// every line sharing a `Reference` together (a "batch") rather than a single line, since a
/// batch is only meaningful — balanced — as a whole; reversing one leg of a balanced posting
/// would leave the ledger unbalanced.
/// </summary>
public interface IGlJournalEntryService
{
    Task<GlJournalEntryReversalResult> ReverseByReferenceAsync(string reference, CancellationToken cancellationToken = default);
}
