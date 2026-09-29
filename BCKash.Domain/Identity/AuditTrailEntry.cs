namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `audit_trail` table. Written exclusively by the EF Core
/// SaveChanges interceptor in BCKash.Infrastructure (FR-SEC-6) — never inserted to
/// directly by application code.
/// </summary>
public class AuditTrailEntry
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? Name { get; set; }
    public int? OfficeId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Id of the record the entry is about (e.g. the client's id when Module is "Client"), so one
    /// record's history can be listed. Unset on older entries and on creations (whose id isn't known
    /// until the insert — the record's own created_at/created_by cover those).
    /// </summary>
    public int? EntityId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
