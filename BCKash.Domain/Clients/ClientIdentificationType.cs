using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>Maps the legacy `client_identification_types` lookup table.</summary>
public class ClientIdentificationType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ClientIdentification> ClientIdentifications { get; set; } = new List<ClientIdentification>();
}
