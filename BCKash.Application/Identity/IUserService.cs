using BCKash.Domain.Identity;

namespace BCKash.Application.Identity;

public enum UserWriteOutcome
{
    Success,
    NotFound,
    EmailInUse,
    UserTypeNotFound,
    OfficeNotFound,

    /// <summary>Onboarding is no longer Pending — already approved/declined.</summary>
    InvalidTransition,

    /// <summary>The acting user's UserClass isn't Initiator (and they aren't a super admin).</summary>
    NotAuthorizedToInitiate,

    /// <summary>The acting user isn't an Authorizer of the same user_type as the record's initiator (and isn't a super admin).</summary>
    NotAuthorizedToAuthorize,

    ReasonRequired,
}

public record UserWriteResult(UserWriteOutcome Outcome, User? User = null);

/// <summary>
/// Staff onboarding and lifecycle management — the maker-checker rule described in
/// docs/staff-onboarding-rbac-spec.md. A super admin (a user whose user_type is
/// <see cref="UserTypeSlugs.SuperAdmin"/>) bypasses every UserClass restriction below and can
/// create/approve/manage directly.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Creates a new staff record with a system-generated temporary password, emailed to
    /// <paramref name="newUser"/>'s address. A super admin's creation is auto-approved
    /// (OnboardingStatus = Approved, immediately able to log in); anyone else's creation
    /// requires UserClass = Initiator and leaves the record Pending until an Authorizer of the
    /// same user_type approves it.
    /// </summary>
    Task<UserWriteResult> CreateAsync(User newUser, string userTypeSlug, CancellationToken cancellationToken = default);

    Task<UserWriteResult> ApproveOnboardingAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserWriteResult> DeclineOnboardingAsync(int userId, string reason, CancellationToken cancellationToken = default);

    Task<UserWriteResult> UpdateAsync(int userId, User updated, CancellationToken cancellationToken = default);

    Task<UserWriteResult> AssignOfficeAsync(int userId, int officeId, CancellationToken cancellationToken = default);

    Task<UserWriteResult> ChangeUserTypeAsync(int userId, string userTypeSlug, CancellationToken cancellationToken = default);

    Task<UserWriteResult> ChangeUserClassAsync(int userId, UserClass userClass, CancellationToken cancellationToken = default);

    Task<UserWriteResult> BlockAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserWriteResult> UnblockAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Generates a new temporary password, emails it, and overwrites the stored hash.</summary>
    Task<UserWriteResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default);
}
