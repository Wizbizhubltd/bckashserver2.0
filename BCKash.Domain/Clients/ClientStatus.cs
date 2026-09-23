namespace BCKash.Domain.Clients;

/// <summary>Matches the legacy `clients.status` enum exactly — values are serialized as these strings.</summary>
public enum ClientStatus
{
    Pending,
    Active,
    Inactive,
    Declined,
    Closed,
}
