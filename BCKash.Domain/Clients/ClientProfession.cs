using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>Maps the legacy `client_profession` lookup table.</summary>
public class ClientProfession : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
