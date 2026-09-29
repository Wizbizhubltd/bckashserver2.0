using BCKash.Domain.Clients;

namespace BCKash.Application.Clients;

/// <summary>What the signed-in user may do with one client — the answers the office portal's action buttons show.</summary>
public record ClientActions(
    bool CanEditDetails,
    bool CanApprove,
    bool CanDecline,
    bool CanDelete,
    bool CanRequestDeletion,
    bool CanMarkSafe,
    bool CanRequestEdit,
    bool EditPrivilegeOpen,
    bool CanReviewEditRequests);

/// <summary>
/// Who may see and work on a client. Visibility follows the office scope. Onboarding documentation
/// (details, photo, documents, guarantors, references, next of kin) belongs to whoever onboarded the
/// client, and locks once the client is approved — reopened only by an edit privilege a controller
/// grants (see ClientEditRequest). Approving or declining belongs to controllers; clearing a
/// high-risk flag to super admins.
/// </summary>
public interface IClientAccess
{
    /// <summary>The client if it exists, isn't deleted and is in the caller's offices; otherwise null.</summary>
    Task<Client?> FindVisibleAsync(int clientId, CancellationToken cancellationToken = default);

    Task<bool> CanDocumentAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>Why the caller can't change the client's documentation right now, or null when they can.</summary>
    Task<string?> DocumentationBlockAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>Whoever onboarded the client — the one who documents them and asks for edit privilege.</summary>
    Task<bool> IsOnboarderAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Call after any change to an approved client's documentation under an edit privilege: the first
    /// one sends the client back to Pending until a controller approves them again.
    /// </summary>
    Task NoteEditedAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes the client's loan or loan application that's still open — awaiting a decision or
    /// disbursement, running, or being rescheduled, including their share of a group loan — or null
    /// when they have none. A client with an active loan can't apply for another.
    /// </summary>
    Task<string?> ActiveLoanAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Whether another (non-deleted) client already has this BVN. No two clients may share one.</summary>
    Task<bool> BvnTakenAsync(string bvn, int? exceptClientId, CancellationToken cancellationToken = default);

    Task<ClientActions> ActionsForAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the client still needs before a controller can approve them — e.g. "1 more guarantor".
    /// Empty when nothing's missing.
    /// </summary>
    Task<IReadOnlyList<string>> ApprovalBlockersAsync(Client client, CancellationToken cancellationToken = default);
}
