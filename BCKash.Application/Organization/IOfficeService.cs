using BCKash.Domain.Organization;

namespace BCKash.Application.Organization;

public enum OfficeWriteOutcome
{
    Success,
    NotFound,
    CircularParent,
    InUseConfirmationRequired,
}

public record OfficeInUseCounts(int ActiveClientCount, int OpenLoanCount);

public record OfficeWriteResult(OfficeWriteOutcome Outcome, Office? Office = null, OfficeInUseCounts? InUse = null);

/// <summary>
/// Office CRUD with its two business rules (BR-ORG-1, Phase 1 acceptance criteria):
/// hierarchy cycle prevention and a confirm-before-deactivate-if-in-use flow. Plain
/// reads (list/get) don't need this — they go straight through BCKashDbContext from
/// the controller, same as Phase 0's SettingsController.
/// </summary>
public interface IOfficeService
{
    Task<OfficeWriteResult> CreateAsync(Office office, CancellationToken cancellationToken = default);

    /// <summary>Applies <paramref name="updated"/>'s editable fields onto the stored office with <paramref name="id"/>.</summary>
    Task<OfficeWriteResult> UpdateAsync(int id, Office updated, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets Active = false. If the office has active clients or open loans, returns
    /// InUseConfirmationRequired (with counts) unless <paramref name="confirm"/> is true.
    /// </summary>
    Task<OfficeWriteResult> DeactivateAsync(int id, bool confirm, CancellationToken cancellationToken = default);

    /// <summary>Sets Active = true — reactivating never needs confirmation, unlike deactivating.</summary>
    Task<OfficeWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default);
}
