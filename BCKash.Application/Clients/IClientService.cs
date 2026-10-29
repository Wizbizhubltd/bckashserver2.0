using BCKash.Domain.Clients;

namespace BCKash.Application.Clients;

public enum ClientWriteOutcome
{
    Success,
    NotFound,

    /// <summary>The requested transition isn't reachable from the client's current status — see <see cref="ClientStatusTransitionRules"/>.</summary>
    InvalidTransition,

    /// <summary>Deactivate/Decline/Close called with a blank reason.</summary>
    ReasonRequired,

    /// <summary>Account-number generation exhausted its collision retries.</summary>
    AccountNumberGenerationFailed,

    /// <summary>Transfer: the target office doesn't exist or is inactive.</summary>
    OfficeNotFound,

    /// <summary>Transfer: the marketer doesn't exist, is blocked, or isn't a marketer.</summary>
    MarketerNotFound,

    /// <summary>Transfer: the marketer works in a different office from the target.</summary>
    MarketerInDifferentOffice,

    /// <summary>Transfer: the client is already in the target office.</summary>
    AlreadyInOffice,

    /// <summary>Transfer: the client has a loan or loan application still open — see IClientAccess.ActiveLoanAsync.</summary>
    HasActiveLoan,

    /// <summary>Transfer: a loan of theirs, or their share of a group loan, is non-performing or written off.</summary>
    Defaulter,
}

public record ClientWriteResult(ClientWriteOutcome Outcome, Client? Client = null);

/// <summary>
/// Client CRUD plus the FR-CLI-2 status lifecycle. Mirrors the <c>IOfficeService</c> shape:
/// entities in/out (not DTOs — the controller maps to/from DTOs), an outcome enum + result
/// record instead of exceptions for expected failure cases.
/// </summary>
public interface IClientService
{
    /// <summary>Generates <see cref="Client.AccountNo"/> server-side — never accepted from the caller. <see cref="Client.OldAccountNo"/> is left exactly as set on <paramref name="client"/> (always null in Phase 2 — no write path exists until Phase 9's migration importer).</summary>
    Task<ClientWriteResult> CreateAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>Applies <paramref name="updated"/>'s editable KYC/address fields onto the stored client. Never touches <see cref="Client.Status"/>, <see cref="Client.AccountNo"/>, <see cref="Client.OldAccountNo"/>, or any transition field — those only change via the transition methods below.</summary>
    Task<ClientWriteResult> UpdateAsync(int id, Client updated, CancellationToken cancellationToken = default);

    Task<ClientWriteResult> ActivateAsync(int id, DateOnly? activatedDate, CancellationToken cancellationToken = default);

    Task<ClientWriteResult> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>From Inactive back to Active. Leaves <see cref="Client.ActivatedDate"/>/<see cref="Client.ActivatedById"/> untouched — FR-CLI-2 tracks reactivation separately via <see cref="Client.ReactivatedDate"/>/<see cref="Client.ReactivatedById"/>.</summary>
    Task<ClientWriteResult> ReactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<ClientWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<ClientWriteResult> CloseAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the client to <paramref name="officeId"/> under <paramref name="marketerId"/> (a marketer in that
    /// office). They leave their current group, since groups belong to one office, and their open savings
    /// accounts follow them. Clients with an open loan or loan application, and defaulters, stay put.
    /// </summary>
    Task<ClientWriteResult> TransferOfficeAsync(int id, int officeId, int marketerId, CancellationToken cancellationToken = default);
}
