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
    DateTime? LastLogin);
