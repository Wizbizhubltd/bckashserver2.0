using BCKash.Domain.GeneralLedger;

namespace BCKash.Application.GeneralLedger;

public enum GlAccountWriteOutcome
{
    Success,
    NotFound,
    CircularParent,
}

public record GlAccountWriteResult(GlAccountWriteOutcome Outcome, GlAccount? Account = null);

/// <summary>
/// Chart-of-accounts CRUD (BR-GL-1, FR-GL-1) — a self-referencing tree via `parent_id`,
/// same shape and cycle-prevention rule as <see cref="BCKash.Application.Organization.IOfficeService"/>.
/// Plain reads go straight through BCKashDbContext from the controller, same convention as Offices.
/// </summary>
public interface IGlAccountService
{
    Task<GlAccountWriteResult> CreateAsync(GlAccount account, CancellationToken cancellationToken = default);

    Task<GlAccountWriteResult> UpdateAsync(int id, GlAccount updated, CancellationToken cancellationToken = default);

    Task<GlAccountWriteResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<GlAccountWriteResult> ActivateAsync(int id, CancellationToken cancellationToken = default);
}
