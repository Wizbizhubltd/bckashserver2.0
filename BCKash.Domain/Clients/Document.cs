using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Maps the legacy `documents` table — a polymorphic attachment referencing a record in one of
/// several tables via <see cref="Type"/> + <see cref="RecordId"/>. <see cref="RecordId"/> is a
/// plain scalar (no navigation) since it can point at many different tables.
/// </summary>
public class Document : IHasTimestamps
{
    public int Id { get; set; }
    public ReferenceEntityType? Type { get; set; }
    public int? RecordId { get; set; }
    public string? Name { get; set; }
    public string? Size { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
