using BCKash.Domain.Clients;

namespace BCKash.Application.Clients;

public enum DeletionOutcome
{
    Success,
    NotFound,

    /// <summary>Approved (or has an approved member) — raise a deletion request instead.</summary>
    RequiresRequest,

    /// <summary>Has loans or loan applications still open, so it can't go yet.</summary>
    HasOpenLoans,
    ReasonRequired,

    /// <summary>A deletion request for it is already waiting for a super admin.</summary>
    AlreadyRequested,

    /// <summary>The request was already approved or rejected.</summary>
    AlreadyReviewed,
}

public record DeletionResult(DeletionOutcome Outcome, DeletionRequest? Request = null);

/// <summary>
/// Deleting clients and groups (see <see cref="ClientDeletionRules"/>). Never-approved ones can be
/// deleted straight away; anything approved needs a <see cref="DeletionRequest"/> a super admin approves.
/// Callers check the caller can see the record first.
/// </summary>
public interface IDeletionService
{
    Task<bool> GroupHasApprovedMemberAsync(int groupId, CancellationToken cancellationToken = default);

    Task<DeletionResult> DeleteClientAsync(int clientId, CancellationToken cancellationToken = default);

    Task<DeletionResult> DeleteGroupAsync(int groupId, CancellationToken cancellationToken = default);

    Task<DeletionResult> RequestAsync(string entityType, int entityId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Super admins: approving carries the deletion out; rejecting leaves the record as it is.</summary>
    Task<DeletionResult> ReviewAsync(int requestId, bool approve, string? note, CancellationToken cancellationToken = default);
}
