using BCKash.SharedKernel;

namespace BCKash.Domain.Identity;

/// <summary>
/// Maps the legacy `users` table. `LegacyPermissionsRaw` is the original free-text
/// `permissions` column, kept read-only for the one-time RBAC migration parser
/// (FRD §1.5) — new code must use <see cref="UserPermissions"/> instead.
/// </summary>
public class User : IHasTimestamps, IAuditable
{
    public int Id { get; set; }

    /// <summary>Legacy column is bigint(20); narrowed to int to match offices.id and support a real FK/navigation.</summary>
    public int? OfficeId { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash — legacy `$2y$...` hashes verify directly, see NFR-4 resolution.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? LegacyPermissionsRaw { get; set; }

    public DateTime? LastLogin { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public bool EnableGoogle2fa { get; set; }
    public bool Blocked { get; set; }
    public string? Google2faSecret { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public bool TimeLimit { get; set; }
    public string? FromTime { get; set; }
    public string? ToTime { get; set; }
    public string? AccessDays { get; set; }

    // New in this pass — the legacy schema has no maker-checker concept. UserClass is the
    // initiator/authorizer/reviewer classification layered on top of a user's "user_type" (the
    // one Role assigned to them whose slug is in UserTypeSlugs.All — see that class's doc
    // comment). CreatedById tracks who initiated this staff record, so an authorizer's user_type
    // can be checked against the initiator's. OnboardingStatus defaults to Approved so no
    // pre-existing user is retroactively locked out; only staff created through the new
    // onboarding flow start Pending. See docs/staff-onboarding-rbac-spec.md.
    public UserClass? UserClass { get; set; }
    public int? CreatedById { get; set; }
    public UserOnboardingStatus OnboardingStatus { get; set; } = UserOnboardingStatus.Approved;
    public int? OnboardingApprovedById { get; set; }
    public DateOnly? OnboardingApprovedDate { get; set; }
    public int? OnboardingDeclinedById { get; set; }
    public DateOnly? OnboardingDeclinedDate { get; set; }
    public string? OnboardingDeclinedReason { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Organization.Office? Office { get; set; }
    public ICollection<RoleUser> RoleUsers { get; set; } = new List<RoleUser>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
