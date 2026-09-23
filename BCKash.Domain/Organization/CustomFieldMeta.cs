using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `custom_fields_meta` table — a tree (`parent_id`) of values scoped to a `custom_field_id` (BRD §6.1).</summary>
public class CustomFieldMeta : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public string? Category { get; set; }
    public int? ParentId { get; set; }
    public int? CustomFieldId { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public CustomFieldMeta? Parent { get; set; }
    public ICollection<CustomFieldMeta> Children { get; set; } = new List<CustomFieldMeta>();
    public CustomField? CustomField { get; set; }
}
