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

    /// <summary>The client has a loan or loan application still open — see IClientAccess.ActiveLoanAsync.</summary>
    HasActiveLoan,

    /// <summary>The client has a non-performing or written-off loan.</summary>
    Defaulter,

    /// <summary>The client and the group are in different offices.</summary>
    DifferentOffice,

    /// <summary>Only pending or active groups take new members.</summary>
    GroupNotOpen,
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

    /// <summary>
    /// Moves a client out of their current group(s) and into <paramref name="groupId"/>. Refused for a
    /// client with an open loan or application, or a defaulter (a non-performing or written-off loan).
    /// </summary>
    Task<MembershipWriteResult> MoveAsync(int clientId, int groupId, CancellationToken cancellationToken = default);
}
