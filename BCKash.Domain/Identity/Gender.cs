namespace BCKash.Domain.Identity;

/// <summary>Matches the legacy `users.gender` enum exactly — values are serialized as these strings.</summary>
public enum Gender
{
    Unspecified,
    Male,
    Female,
    Other,
}
