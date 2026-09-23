namespace BCKash.Application.GeneralLedger;

/// <summary>
/// FR-GL-4: shared enforcement used by every GL-posting path (system-posted loan entries,
/// manual entries, office transfers) — a date on/before an active (non-reopened) closure for
/// that office can't be posted to. Not exposed via the API on its own.
/// </summary>
public interface IGlClosureGuard
{
    Task<bool> IsDatePostableAsync(int? officeId, DateOnly date, CancellationToken cancellationToken = default);
}
