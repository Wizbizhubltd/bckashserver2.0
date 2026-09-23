using BCKash.Domain.Groups;

namespace BCKash.Application.Groups;

public enum GroupWriteOutcome
{
    Success,
    NotFound,
    InvalidTransition,
    ReasonRequired,
}

public record GroupWriteResult(GroupWriteOutcome Outcome, Group? Group = null);

/// <summary>
/// Group CRUD plus the FR-GRP-1 status lifecycle. Mirrors <c>IClientService</c>'s shape —
/// entities in/out, an outcome enum + result record instead of exceptions.
/// </summary>
public interface IGroupService
{
    Task<GroupWriteResult> CreateAsync(Group group, CancellationToken cancellationToken = default);

    /// <summary>Applies <paramref name="updated"/>'s editable fields onto the stored group. Never touches <see cref="Group.Status"/> or any transition field.</summary>
    Task<GroupWriteResult> UpdateAsync(int id, Group updated, CancellationToken cancellationToken = default);

    Task<GroupWriteResult> ActivateAsync(int id, DateOnly? activatedDate, CancellationToken cancellationToken = default);

    Task<GroupWriteResult> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<GroupWriteResult> ReactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<GroupWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<GroupWriteResult> CloseAsync(int id, string reason, CancellationToken cancellationToken = default);
}
