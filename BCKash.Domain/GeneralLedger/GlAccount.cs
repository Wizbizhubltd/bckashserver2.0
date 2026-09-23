using BCKash.SharedKernel;

namespace BCKash.Domain.GeneralLedger;

/// <summary>Legacy `gl_accounts.account_type`.</summary>
public enum GlAccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense
}

/// <summary>
/// Maps the legacy `gl_accounts` table (BRD §6.7) — the chart of accounts, a
/// self-referencing tree via `parent_id` (same pattern as
/// <see cref="BCKash.Domain.Identity.Permission"/>). Legacy default 0 for root is
/// mapped to null here.
/// </summary>
public class GlAccount : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? ParentId { get; set; }
    public string? GlCode { get; set; }
    public GlAccountType AccountType { get; set; }
    public bool Active { get; set; } = true;
    public bool ManualEntries { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public GlAccount? Parent { get; set; }
    public ICollection<GlAccount> Children { get; set; } = new List<GlAccount>();
    public ICollection<GlJournalEntry> JournalEntries { get; set; } = new List<GlJournalEntry>();
}
