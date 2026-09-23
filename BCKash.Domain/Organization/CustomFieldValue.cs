using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>
/// New additive table (not in the legacy schema — see Phase 1 plan notes on FR-ORG-6):
/// the legacy `custom_fields_meta` table has no record-reference or value column, so it
/// cannot be what stores "a value captured against a record" as the FRD assumed. This
/// table is the real value store — <see cref="EntityType"/>/<see cref="EntityId"/>
/// identify the record the value belongs to (e.g. "Client"/123), matching the same
/// polymorphic-reference pattern already used by `documents`/`notes` (BR-CLI-4).
/// </summary>
public class CustomFieldValue : IHasTimestamps
{
    public int Id { get; set; }
    public int CustomFieldId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? Value { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public CustomField CustomField { get; set; } = null!;
}
