using BCKash.Domain.Groups;

namespace BCKash.Api.Contracts;

// ---- Groups ----

public record GroupListItemResponse(
    int Id, string? AccountNo, string? Name, int? OfficeId, int? StaffId, GroupStatus Status, DateOnly? JoinedDate);

public record GroupResponse(
    int Id, string? OldGroupId, int? OfficeId, string? Name, string? AccountNo, string? ExternalId, int? StaffId,
    DateOnly? JoinedDate, GroupStatus Status,
    DateOnly? ActivatedDate, int? ActivatedById, DateOnly? ReactivatedDate, int? ReactivatedById,
    DateOnly? DeclinedDate, int? DeclinedById, string? DeclinedReason,
    DateOnly? ClosedDate, int? ClosedById, string? ClosedReason,
    DateOnly? InactiveDate, int? InactiveById, string? InactiveReason,
    string? Mobile, string? Phone, string? Email,
    string? Street, string? Ward, string? District, string? Region, string? Address, string? Notes);

public record CreateGroupRequest(
    int? OfficeId, string? Name, string? ExternalId, int? StaffId, DateOnly? JoinedDate,
    string? Mobile, string? Phone, string? Email,
    string? Street, string? Ward, string? District, string? Region, string? Address, string? Notes);

public record UpdateGroupRequest(
    int? OfficeId, string? Name, string? ExternalId, int? StaffId, DateOnly? JoinedDate,
    string? Mobile, string? Phone, string? Email,
    string? Street, string? Ward, string? District, string? Region, string? Address, string? Notes);

public record ActivateGroupRequest(DateOnly? ActivatedDate);

// ---- Membership ----

/// <summary><see cref="ClientDisplayName"/>/<see cref="ClientAccountNo"/> are joined in from the referenced Client for a useful roster row.</summary>
public record GroupMemberResponse(
    int Id, int? ClientId, string? ClientDisplayName, string? ClientAccountNo,
    DateTime? CreatedAt, int? CreatedById, DateTime? RemovedAt, int? RemovedById);

public record AddGroupMemberRequest(int ClientId);
