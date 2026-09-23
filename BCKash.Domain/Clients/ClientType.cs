namespace BCKash.Domain.Clients;

/// <summary>Matches the legacy `clients.client_type` enum exactly — values are serialized as these strings.</summary>
public enum ClientType
{
    Individual,
    Business,
    Ngo,
    Other,
}
