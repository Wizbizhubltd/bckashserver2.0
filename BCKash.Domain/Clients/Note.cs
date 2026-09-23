using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Maps the legacy `notes` table — a polymorphic note attached to a record in one of several
/// tables via <see cref="Type"/> + <see cref="ReferenceId"/>. <see cref="ReferenceId"/> is a
/// plain scalar (no navigation) since it can point at many different tables.
/// </summary>
public class Note : IHasTimestamps
{
    public int Id { get; set; }
    public int? ReferenceId { get; set; }
    public ReferenceEntityType? Type { get; set; }
    public int? CreatedById { get; set; }
    public int? ModifiedById { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
