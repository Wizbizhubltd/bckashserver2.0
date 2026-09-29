using BCKash.Application.Clients;

namespace BCKash.Api.Contracts;

public record BvnCheckRequest(string Bvn, string FullName, string? Phone);

public record BvnDetailsResponse(string? FirstName, string? MiddleName, string? LastName, string? Phone, string? BirthDate, string? Gender, string? Photo);

/// <summary>
/// A BVN lookup. <c>VerificationId</c> goes back with the client when they're onboarded.
/// <c>Matches</c> is false when any compared field differs — the staff member then chooses whose
/// details to keep.
/// </summary>
public record BvnCheckResponse(int VerificationId, bool Matches, IReadOnlyList<BvnFieldComparison> Comparisons, BvnDetailsResponse FromBvn);

public record OnboardClientRequest(
    string FullName,
    string? Email,
    string? Phone,
    string Bvn,
    int BvnVerificationId,
    /// <summary>"bvn" or "client" — required only when the BVN lookup didn't match.</summary>
    string? DetailsSource,
    string? OverrideReason);

public record OnboardSingleClientRequest(int? OfficeId, OnboardClientRequest Client);

public record OnboardGroupDetailsRequest(string Name, string? Phone, string? Email, string? Address);

/// <summary>At least three members; the first three become the group's leader, assistant and organizer.</summary>
public record OnboardGroupRequest(int? OfficeId, OnboardGroupDetailsRequest Group, IReadOnlyList<OnboardClientRequest> Members);

public record OnboardedClientResponse(int Id, string? AccountNo, string? DisplayName, bool IsHighRisk);

public record OnboardingResponse(int? GroupId, IReadOnlyList<OnboardedClientResponse> Clients);
