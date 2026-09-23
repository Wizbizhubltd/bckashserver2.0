namespace BCKash.Domain.Groups;

/// <summary>Matches the legacy `groups.status` enum exactly — values are serialized as these strings.</summary>
public enum GroupStatus
{
    Pending,
    Active,
    Inactive,
    Declined,
    Closed,
}
