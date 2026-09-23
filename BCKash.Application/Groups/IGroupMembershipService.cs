using BCKash.Domain.Groups;

namespace BCKash.Application.Groups;

public enum MembershipWriteOutcome
{
    Success,
    GroupNotFound,
    ClientNotFound,

    /// <summary>The client already has an active (non-removed) membership in this group.</summary>
    AlreadyAMember,

    MembershipNotFound,
}

public record MembershipWriteResult(MembershipWriteOutcome Outcome, GroupClient? Membership = null);

/// <summary>
/// Group membership add/remove (BR-GRP-2). "Remove" soft-removes (sets RemovedAt/RemovedById on
/// the specific GroupClient row) rather than deleting — see GroupClient's doc comment for why.
/// </summary>
public interface IGroupMembershipService
{
    Task<MembershipWriteResult> AddAsync(int groupId, int clientId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="groupClientId"/> is the membership record's own id (not the client's id) — a client removed and re-added has more than one historical row for the same group.</summary>
    Task<MembershipWriteResult> RemoveAsync(int groupId, int groupClientId, CancellationToken cancellationToken = default);
}
