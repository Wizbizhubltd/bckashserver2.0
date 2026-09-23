using BCKash.Domain.Clients;
using BCKash.Domain.Organization;
using BCKash.SharedKernel;

namespace BCKash.Domain.Groups;

/// <summary>
/// Maps the legacy `groups` table (BRD §6.4) — a lending group of clients. Implements
/// <see cref="IAuditable"/> (added in Phase 3, alongside <see cref="Clients.Client"/>) so every
/// create/update/status-transition is recorded in <c>audit_trail</c> automatically via
/// <see cref="BCKash.SharedKernel.IAuditable"/>'s interceptor, satisfying FR-GRP-1's
/// "each transition logged".
/// </summary>
public class Group : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public string? OldGroupId { get; set; }
    public int? OfficeId { get; set; }
    public string? Name { get; set; }
    public string? AccountNo { get; set; }
    public string? ExternalId { get; set; }
    public int? StaffId { get; set; }
    public DateOnly? JoinedDate { get; set; }
    public DateOnly? ActivatedDate { get; set; }
    public DateOnly? ReactivatedDate { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public string? DeclinedReason { get; set; }
    public string? ClosedReason { get; set; }
    public DateOnly? ClosedDate { get; set; }

    /// <summary>New in Phase 3 — the legacy `groups` table has no inactive_reason/inactive_date/inactive_by_id columns, unlike `clients`, but FR-GRP-1 requires the inactive transition to carry a logged reason/actor/date just like Client's does.</summary>
    public string? InactiveReason { get; set; }
    public DateOnly? InactiveDate { get; set; }
    public int? InactiveById { get; set; }

    public int? CreatedById { get; set; }
    public int? ActivatedById { get; set; }
    public int? ReactivatedById { get; set; }
    public int? DeclinedById { get; set; }
    public int? ClosedById { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Street { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public GroupStatus Status { get; set; } = GroupStatus.Pending;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Office? Office { get; set; }

    public ICollection<GroupClient> GroupClients { get; set; } = new List<GroupClient>();
    public ICollection<GroupUser> GroupUsers { get; set; } = new List<GroupUser>();
}
