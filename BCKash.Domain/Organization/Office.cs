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

    /// <summary>Generated once on creation (see <see cref="OfficeCodeFormat"/>) and never edited.</summary>
    public string? OfficeCode { get; set; }

    // Nullable because offices created before these fields existed have none; the API requires
    // all four whenever an office is created or edited.
    public int? StateId { get; set; }
    public int? LgaId { get; set; }
    public int? CityId { get; set; }
    public int? ZoneId { get; set; }
    public int? CreatedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Office? Parent { get; set; }
    public State? State { get; set; }
    public Lga? Lga { get; set; }
    public City? City { get; set; }
    public Zone? Zone { get; set; }
}
