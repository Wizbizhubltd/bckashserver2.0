using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `custom_fields` table — BRD §6.1.</summary>
public class CustomField : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public CustomFieldType FieldType { get; set; } = Organization.CustomFieldType.Textfield;
    public bool Required { get; set; }
    public string? RadioBoxValues { get; set; }
    public string? CheckboxValues { get; set; }
    public string? SelectValues { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CustomFieldMeta> Meta { get; set; } = new List<CustomFieldMeta>();
}
