using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>Maps the legacy `client_identifications` table — an identification document attached to a client.</summary>
public class ClientIdentification : IHasTimestamps
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public int? ClientIdentificationTypeId { get; set; }
    public string? Name { get; set; }
    public bool Active { get; set; } = true;
    public string? Notes { get; set; }
    public string? Attachment { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public ClientIdentificationType? ClientIdentificationType { get; set; }
}
