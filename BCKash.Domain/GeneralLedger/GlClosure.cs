using BCKash.SharedKernel;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Maps the legacy `gl_closures` table (BRD §6.7) — a period-end accounting closure
/// per office. `office_id`, `created_by_id`, `modified_by_id` reference tables outside
/// this table group, so they stay plain scalar columns.
/// </summary>
public class GlClosure : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? OfficeId { get; set; }
    public int? CreatedById { get; set; }
    public DateOnly ClosingDate { get; set; }
    public int? ModifiedById { get; set; }
    public string? GlReference { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// New in Phase 6 — the legacy schema has no "reopen" concept at all (a closure is just a
    /// row). Reopening is modeled by marking the row reopened rather than deleting it, so the
    /// full close/reopen history stays queryable (same additive pattern as Phase 3's
    /// `GroupClient.RemovedAt`/`RemovedById`). A closure only blocks postings while this is null.
    /// </summary>
    public DateTime? ReopenedAt { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ReopenedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<GlJournalEntry> JournalEntries { get; set; } = new List<GlJournalEntry>();
}
