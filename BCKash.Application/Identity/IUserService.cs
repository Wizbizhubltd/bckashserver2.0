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

    /// <summary>The office isn't one the acting user works in (see <see cref="IOfficeScope"/>).</summary>
    OfficeOutOfScope,

    /// <summary>The acting user may only create or assign user_types ranked below their own (see <see cref="UserTypeSlugs.Rank"/>).</summary>
    UserTypeNotPermitted,

    /// <summary>The target staff member isn't ranked below the acting user.</summary>
    NotAuthorizedToManage,

    /// <summary>Zones can only be assigned to a director.</summary>
    NotADirector,

    ZoneNotFound,

    /// <summary>The staff member is blocked — bulk actions only apply to active staff.</summary>
    NotActive,

    /// <summary>Super admins are exempt from the staff directory's bulk actions.</summary>
    SuperAdminExcluded,

    /// <summary>A client or group the staff member looks after has a running loan, so they can't leave their office.</summary>
    HasActiveLoans,

    /// <summary>The staff member already works in the office they're being transferred to.</summary>
    AlreadyInOffice,
}

public record UserWriteResult(UserWriteOutcome Outcome, User? User = null);

/// <summary>
/// Staff onboarding and lifecycle management. Below a super admin, every action is also limited
/// to staff in the acting user's offices (<see cref="IOfficeScope"/>) who rank below them
/// (<see cref="UserTypeSlugs.Rank"/>) — a manager manages marketers, a controller managers and
/// marketers, a director everyone under a director in their zones.
///
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

    /// <summary>
    /// Super admins only (callers enforce it): edits a staff member's whole record — email, personal
    /// details, notes and the onboarding details they'd otherwise fill in themselves. Office, role,
    /// class, status and password keep their own actions.
    /// </summary>
    Task<UserWriteResult> UpdateRecordAsync(int userId, User updated, CancellationToken cancellationToken = default);

    Task<UserWriteResult> AssignOfficeAsync(int userId, int officeId, CancellationToken cancellationToken = default);

    Task<UserWriteResult> ChangeUserTypeAsync(int userId, string userTypeSlug, CancellationToken cancellationToken = default);

    Task<UserWriteResult> ChangeUserClassAsync(int userId, UserClass userClass, CancellationToken cancellationToken = default);

    Task<UserWriteResult> BlockAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserWriteResult> UnblockAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Generates a new temporary password, emails it, and overwrites the stored hash.</summary>
    Task<UserWriteResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk action: replaces an active staff member's password with an 8-character temporary one,
    /// emails it with the staff-portal address, and makes them change it on their next sign-in.
    /// Super admins are exempt.
    /// </summary>
    Task<UserWriteResult> ForcePasswordResetAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk action: moves an active staff member to another office and emails them about it. Refused
    /// while any client or group they look after (or any loan they're the officer on) has a running loan.
    /// Super admins are exempt.
    /// </summary>
    Task<UserWriteResult> TransferOfficeAsync(int userId, int officeId, CancellationToken cancellationToken = default);

    /// <summary>Bulk action: disables (blocks) an active staff member so they can no longer sign in. Super admins are exempt.</summary>
    Task<UserWriteResult> DisableAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The signed-in user updating their own profile and onboarding details (names, contact, next of kin, bank).</summary>
    Task<UserWriteResult> UpdateOwnProfileAsync(User updated, CancellationToken cancellationToken = default);

    /// <summary>Replaces the zones a director oversees. Callers restrict this to super admins.</summary>
    Task<UserWriteResult> AssignZonesAsync(int userId, IReadOnlyCollection<int> zoneIds, CancellationToken cancellationToken = default);
}
