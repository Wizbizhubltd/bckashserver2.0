using BCKash.Domain.Clients;
using BCKash.Domain.Groups;

namespace BCKash.Application.Clients;

/// <summary>One field compared between what the staff member typed and what the BVN is registered to.</summary>
public record BvnFieldComparison(string Field, string? Given, string? FromBvn, bool Matches);

public enum BvnCheckOutcome
{
    Checked,
    InvalidBvn,

    /// <summary>First and last name are both needed to look a BVN up.</summary>
    NameRequired,
    NotFound,

    /// <summary>A client with this BVN is already on the platform.</summary>
    AlreadyRegistered,
    ProviderUnavailable,
}

public record BvnCheckResult(
    BvnCheckOutcome Outcome,
    int? VerificationId = null,
    bool Matches = false,
    IReadOnlyList<BvnFieldComparison>? Comparisons = null,
    BvnLookupResult? FromBvn = null,
    string? Error = null);

/// <summary>
/// One client being onboarded. <c>DetailsSource</c> matters only when the BVN lookup didn't match:
/// "bvn" saves the provider's details, "client" keeps what was typed — which needs
/// <c>OverrideReason</c> and flags the client high risk.
/// </summary>
public record OnboardClientInput(
    string FullName,
    string? Email,
    string? Phone,
    string Bvn,
    int BvnVerificationId,
    string? DetailsSource,
    string? OverrideReason);

public record OnboardGroupInput(string Name, string? Phone, string? Email, string? Address);

public enum OnboardingOutcome
{
    Success,

    /// <summary>Something about the request is wrong; <see cref="OnboardingResult.Errors"/> says what, per member where it applies.</summary>
    Invalid,
    OfficeOutOfScope,
    NotPermitted,
    AccountNumberGenerationFailed,
}

/// <summary><c>Errors</c> keys are "group", "office", "marketer", or "members[i]" for the i-th client (0-based).</summary>
public record OnboardingResult(
    OnboardingOutcome Outcome,
    IReadOnlyList<Client>? Clients = null,
    Group? Group = null,
    IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>
/// Onboards clients from the office portal — one at a time, or as a group of at least
/// <see cref="ClientOnboardingRules.MinimumGroupSize"/>. Managers and marketers onboard into their own
/// office(s); a super admin onboards into any office and must name a marketer there (<c>marketerId</c>)
/// who manages the clients from then on — for everyone else <c>marketerId</c> is ignored.
/// Every client's BVN is looked up first (<see cref="CheckBvnAsync"/>); onboarding then refers to that
/// lookup, and everything for one onboarding is saved together or not at all.
/// </summary>
public interface IClientOnboardingService
{
    Task<BvnCheckResult> CheckBvnAsync(string bvn, string fullName, string? phone, CancellationToken cancellationToken = default);

    Task<OnboardingResult> OnboardClientAsync(int? officeId, int? marketerId, OnboardClientInput client, CancellationToken cancellationToken = default);

    Task<OnboardingResult> OnboardGroupAsync(int? officeId, int? marketerId, OnboardGroupInput group, IReadOnlyList<OnboardClientInput> members, CancellationToken cancellationToken = default);
}
