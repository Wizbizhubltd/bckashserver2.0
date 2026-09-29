using BCKash.Domain.Groups;

namespace BCKash.Api.Contracts;

// ---- Groups ----

public record GroupListItemResponse(
    int Id, string? AccountNo, string? Name, int? OfficeId, int? StaffId, GroupStatus Status, DateOnly? JoinedDate,
    string? StaffName = null, int MemberCount = 0);

/// <summary>A bulk action on the ticked groups.</summary>
public record BulkGroupsRequest(IReadOnlyList<int> GroupIds);

/// <summary>Bulk: hands the ticked groups, and their current members, to a marketer.</summary>
public record BulkReassignMarketerRequest(IReadOnlyList<int> GroupIds, int MarketerId);

/// <summary>Bulk: disables the ticked active groups, with the reason recorded on each.</summary>
public record BulkDeactivateGroupsRequest(IReadOnlyList<int> GroupIds, string Reason);

/// <summary>Bulk: moves the ticked clients into the group in the route.</summary>
public record BulkMoveClientsRequest(IReadOnlyList<int> ClientIds);

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
    DateTime? CreatedAt, int? CreatedById, DateTime? RemovedAt, int? RemovedById,
    string? Role = null, Domain.Clients.ClientStatus? ClientStatus = null, bool ClientIsHighRisk = false);

/// <summary>One member on the group page, with their share of the group's loans.</summary>
public record GroupSummaryMemberResponse(
    int ClientId, string? DisplayName, string? AccountNo, Domain.Clients.ClientStatus Status, bool IsHighRisk, string? Role,
    decimal LoanAmount, decimal PendingRepayment);

/// <summary>
/// The group page's figures: loan totals across all members (amounts approved, and what's still to
/// be repaid on running loans), member counts by approval, the roster (leader, assistant, organizer
/// first), and what the viewer may do.
/// </summary>
public record GroupSummaryResponse(
    decimal CumulativeLoanAmount,
    int CumulativeLoanCount,
    decimal PendingRepaymentAmount,
    int TotalMembers,
    int ApprovedMembers,
    int PendingMembers,
    IReadOnlyList<GroupSummaryMemberResponse> Members,
    bool CanDelete,
    bool CanRequestDeletion,
    bool PendingDeletionRequest,
    string? CreatedByName,
    string? OfficeName,
    IReadOnlyList<GroupLoanRecordResponse> LoanRecords);

/// <summary>
/// One loan or loan application on the group page: a member's own, their share of a group loan, or
/// the group's. <c>Kind</c> is "loan" or "application"; <c>Status</c> is the loan's or application's.
/// </summary>
public record GroupLoanRecordResponse(
    string Kind, int Id, int? ClientId, string? ClientName, string? Reference, string? LoanProductName,
    decimal Amount, string Status, DateOnly? Date);

public record AddGroupMemberRequest(int ClientId);
