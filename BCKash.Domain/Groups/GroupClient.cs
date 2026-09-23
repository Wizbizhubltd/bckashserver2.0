using BCKash.Domain.Clients;
using BCKash.SharedKernel;

namespace BCKash.Domain.Groups;

/// <summary>
/// Maps the legacy `group_clients` table — links a client to a group. Has its own surrogate
/// `id` primary key in the legacy schema (not a composite key).
///
/// <see cref="CreatedById"/>, <see cref="RemovedAt"/>, and <see cref="RemovedById"/> are new in
/// Phase 3 — the legacy table has no actor or removal-tracking columns at all. Removing a
/// member sets <see cref="RemovedAt"/>/<see cref="RemovedById"/> rather than deleting the row,
/// so the full membership timeline (including past memberships) stays queryable per BR-GRP-2's
/// "tracking historical membership"; the current roster is `WHERE RemovedAt IS NULL`. A client
/// removed and later re-added gets a brand-new row, never a revived one.
/// </summary>
public class GroupClient : IHasTimestamps
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public int? ClientId { get; set; }
    public string? OldGroupId { get; set; }
    public string? OldClientId { get; set; }
    public int? CreatedById { get; set; }
    public DateTime? RemovedAt { get; set; }
    public int? RemovedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Group? Group { get; set; }
    public Client? Client { get; set; }
}
