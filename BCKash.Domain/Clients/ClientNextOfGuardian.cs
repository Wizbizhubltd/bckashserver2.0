using BCKash.Domain.Identity;
using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// Maps the legacy `client_next_of_gaur` table (the legacy name is a typo for "guardian";
/// the table name itself is kept as-is via <c>ToTable</c>).
/// </summary>
public class ClientNextOfGuardian : IHasTimestamps
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public int? ClientRelationshipId { get; set; }
    public string? Qualification { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Ward { get; set; }
    public string? Street { get; set; }
    public string? District { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public string? Picture { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Gender? Gender { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public ClientRelationship? ClientRelationship { get; set; }
}
