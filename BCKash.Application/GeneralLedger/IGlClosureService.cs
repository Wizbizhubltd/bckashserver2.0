using BCKash.Domain.GeneralLedger;

namespace BCKash.Application.GeneralLedger;

public enum GlClosureWriteOutcome
{
    Success,
    NotFound,
    AlreadyClosed,
    AlreadyReopened,
}

public record GlClosureWriteResult(GlClosureWriteOutcome Outcome, GlClosure? Closure = null);

/// <summary>
/// FR-GL-4: period-end closures per office. Closing blocks postings on/before the closing
/// date (enforced by <see cref="IGlClosureGuard"/>); reopening is a privileged, audited action
/// — see <c>gl.closure-reopen</c> permission slug and <see cref="GlClosure.ReopenedAt"/>.
/// </summary>
public interface IGlClosureService
{
    /// <summary>Rejects with AlreadyClosed if an active (non-reopened) closure already covers this office at/after the requested date — closures only move forward.</summary>
    Task<GlClosureWriteResult> CloseAsync(int? officeId, DateOnly closingDate, string? notes, CancellationToken cancellationToken = default);

    Task<GlClosureWriteResult> ReopenAsync(int id, string? notes, CancellationToken cancellationToken = default);
}
