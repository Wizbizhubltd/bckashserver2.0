using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `offices` table — branch/office hierarchy (BR-ORG-1).</summary>
public class Office : IHasTimestamps, ISoftDelete, IAuditable
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? ParentId { get; set; }
    public string? ExternalId { get; set; }
    public DateOnly? OpeningDate { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public int? ManagerId { get; set; }
    public bool Active { get; set; } = true;
    public bool DefaultOffice { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Office? Parent { get; set; }
}
