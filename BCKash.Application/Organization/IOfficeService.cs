using BCKash.Domain.Organization;

namespace BCKash.Application.Organization;

public enum OfficeWriteOutcome
{
    Success,
    NotFound,
    CircularParent,
    InUseConfirmationRequired,

    /// <summary>State, LGA, city or zone is missing.</summary>
    LocationRequired,

    /// <summary>The LGA isn't in the chosen state, the city isn't in the chosen LGA, or one of them doesn't exist.</summary>
    InvalidLocation,
    ZoneNotFound,
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
    /// <summary>Also generates the office's <see cref="Office.OfficeCode"/> and records who created it and when.</summary>
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
