namespace BCKash.Domain.Clients;

/// <summary>Matches the legacy `clients.marital_status` enum exactly — values are serialized as these strings.</summary>
public enum MaritalStatus
{
    Unspecified,
    Married,
    Single,
    Divorced,
    Widowed,
}
