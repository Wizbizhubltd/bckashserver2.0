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
    string? UpdatedByName);

/// <summary>One audit-trail entry recorded against a staff member's actions.</summary>
public record UserActivityResponse(int Id, string? Module, string? Action, string? Notes, int? OfficeId, DateTime? CreatedAt);

// ---- Roles & permissions ----

/// <summary>A permission a role can be granted — see PermissionCatalog.</summary>
public record PermissionResponse(string Slug, string Area, string Name, string Description);

/// <summary><c>Locked</c>: super admin always has every permission and can't be edited.</summary>
public record RoleResponse(int Id, string Slug, string Name, int StaffCount, bool Locked, IReadOnlyList<string> Permissions);

public record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);
