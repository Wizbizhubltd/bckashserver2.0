namespace BCKash.SharedKernel;

/// <summary>
/// Who is making the current request. Implemented in BCKash.Api from the JWT claims
/// and injected wherever Application/Infrastructure code needs "who did this"
/// (audit trail, GL posting actor, permission checks).
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>Null for unauthenticated requests (e.g. the login endpoint itself).</summary>
    int? UserId { get; }

    long? OfficeId { get; }

    bool IsAuthenticated { get; }
}
