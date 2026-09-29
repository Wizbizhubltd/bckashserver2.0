namespace BCKash.Application.Clients;

public enum BvnLookupOutcome
{
    Found,

    /// <summary>The provider has no record of this BVN.</summary>
    NotFound,

    /// <summary>The provider couldn't be reached, refused our credentials, or isn't configured.</summary>
    Unavailable,
}

/// <summary>What the BVN is registered to, per the provider. <c>Gender</c> and <c>Photo</c> (a base64 JPEG) only when the provider returns them.</summary>
public record BvnLookupResult(
    BvnLookupOutcome Outcome,
    string? FirstName = null,
    string? MiddleName = null,
    string? LastName = null,
    string? Phone = null,
    string? BirthDate = null,
    string? Gender = null,
    string? Photo = null,
    string? Error = null);

/// <summary>Looks a BVN up with the identity provider (the BC Kash MFB core gateway) during client onboarding.</summary>
public interface IBvnVerificationProvider
{
    /// <param name="middleName">What the staff member typed, if any; only a mock provider uses it.</param>
    /// <param name="phone">What the staff member typed, if any; only a mock provider uses it.</param>
    Task<BvnLookupResult> LookupAsync(string bvn, string firstName, string? middleName, string lastName, string? phone, CancellationToken cancellationToken = default);
}
