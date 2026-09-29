using BCKash.Domain.Identity;

namespace BCKash.Api.Contracts;

public record CreateUserRequest(
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    int? OfficeId,
    string UserTypeSlug,
    UserClass UserClass,
    Gender? Gender,
    string? Address,
    string? Notes);

public record UpdateUserRequest(string? FirstName, string? LastName, string? Phone, string? Address, string? Notes, Gender? Gender);

public record AssignOfficeRequest(int OfficeId);

/// <summary>The signed-in user's own profile and onboarding details.</summary>
public record UpdateMyProfileRequest(
    string? FirstName,
    string? LastName,
    string? Phone,
    Gender? Gender,
    string? Address,
    DateOnly? DateOfBirth,
    string? NextOfKinName,
    string? NextOfKinPhone,
    string? NextOfKinRelationship,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);

/// <summary>A super admin's edit of a staff member's whole record (office, role, class and status have their own actions).</summary>
public record UpdateStaffRecordRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    Gender? Gender,
    string? Address,
    string? Notes,
    DateOnly? DateOfBirth,
    string? NextOfKinName,
    string? NextOfKinPhone,
    string? NextOfKinRelationship,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);

/// <summary>Replaces every zone a director oversees.</summary>
public record AssignZonesRequest(IReadOnlyList<int> ZoneIds);

public record UserZoneResponse(int Id, string Name);

/// <summary>
/// Bulk forced password reset: the ticked staff, or — with <c>All</c> — every staff member matching
/// the list filters (the same ones GET /users takes), across every page.
/// </summary>
public record BulkResetPasswordRequest(
    IReadOnlyList<int>? UserIds,
    bool All,
    int? OfficeId,
    string? UserType,
    UserOnboardingStatus? OnboardingStatus,
    string? Search);

/// <summary>A bulk action on the ticked staff.</summary>
public record BulkStaffIdsRequest(IReadOnlyList<int> UserIds);

/// <summary>Bulk transfer of the ticked staff to another office.</summary>
public record BulkTransferRequest(IReadOnlyList<int> UserIds, int OfficeId);

/// <summary>A staff member a bulk action left alone, and why.</summary>
public record BulkActionSkip(int Id, string Name, string Reason);

public record BulkActionResponse(int Succeeded, IReadOnlyList<BulkActionSkip> Skipped);

public record ChangeUserTypeRequest(string UserTypeSlug);

public record ChangeUserClassRequest(UserClass UserClass);

public record UserResponse(
    int Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    int? OfficeId,
    string? OfficeName,
    string? UserType,
    UserClass? UserClass,
    bool Blocked,
    UserOnboardingStatus OnboardingStatus,
    int? CreatedById,
    int? OnboardingApprovedById,
    DateOnly? OnboardingApprovedDate,
    int? OnboardingDeclinedById,
    DateOnly? OnboardingDeclinedDate,
    string? OnboardingDeclinedReason,
    DateTime? LastLogin,
    Gender Gender,
    string? Address,
    string? Notes,
    DateTime? CreatedAt,
    string? CreatedByName,
    string? OnboardingApprovedByName,
    DateTime? UpdatedAt,
    string? UpdatedByName,
    DateOnly? DateOfBirth,
    string? NextOfKinName,
    string? NextOfKinPhone,
    string? NextOfKinRelationship,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    IReadOnlyList<UserZoneResponse> Zones,
    /// <summary>The office-portal modules ticked for this staff member's role, in catalogue order.</summary>
    IReadOnlyList<string> Modules,
    /// <summary>Profile fields the staff member still has to fill in; empty once onboarding details are complete.</summary>
    IReadOnlyList<string> MissingProfileFields,
    bool ProfileComplete,
    /// <summary>Still on a temporary password an admin gave them — they haven't reset it yet.</summary>
    bool MustChangePassword = false,
    /// <summary>When an admin last forced a password reset.</summary>
    DateTime? PasswordResetRequestedAt = null,
    /// <summary>When the staff member last set their own password.</summary>
    DateTime? PasswordChangedAt = null);

/// <summary>One audit-trail entry recorded against a staff member's actions.</summary>
public record UserActivityResponse(int Id, string? Module, string? Action, string? Notes, int? OfficeId, DateTime? CreatedAt);

// ---- Roles & permissions ----

/// <summary>A permission a role can be granted — see PermissionCatalog.</summary>
public record PermissionResponse(string Slug, string Area, string Name, string Description);

/// <summary>
/// <c>Locked</c>: super admin always has every permission and can't be edited. <c>Modules</c>: the
/// office-portal modules ticked for the role (always empty for super admin, who can't use that portal).
/// </summary>
public record RoleResponse(int Id, string Slug, string Name, int StaffCount, bool Locked, IReadOnlyList<string> Permissions, IReadOnlyList<string> Modules);

/// <summary>An office-portal module a super admin can tick for a role — see OfficePortalModules.</summary>
public record ModuleResponse(string Slug, string Name, string Description);

public record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);
