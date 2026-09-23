using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>Maps the legacy `client_relationships` lookup table (e.g. spouse, parent, sibling).</summary>
public class ClientRelationship : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ClientNextOfKin> NextOfKin { get; set; } = new List<ClientNextOfKin>();
    public ICollection<ClientNextOfGuardian> NextOfGuardians { get; set; } = new List<ClientNextOfGuardian>();
}
