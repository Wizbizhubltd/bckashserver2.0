namespace BCKash.Application.Savings;

public record SavingsInterestAccrualResult(bool Accrued, decimal InterestAccrued);

public record SavingsInterestPostingResult(bool Posted, decimal InterestPosted);

/// <summary>
/// FR-SAV-4's interest engine. There is no job scheduler anywhere in this codebase (the same
/// situation Phase 5's NPA recompute hit) — this is exposed as an idempotent, on-demand
/// operation safe to call repeatedly: each method only acts on an account whose next
/// calculation/posting date is actually due, so calling it more often than necessary is a
/// no-op, not a double-accrual. <c>RunDueAsync</c> is the closest thing to "the job," triggered
/// explicitly via <c>POST /api/v1/savings-interest/run</c> rather than on a real schedule.
/// </summary>
public interface ISavingsInterestPostingService
{
    Task<SavingsInterestAccrualResult> AccrueAsync(int accountId, DateOnly asOfDate, CancellationToken cancellationToken = default);

    Task<SavingsInterestPostingResult> PostAsync(int accountId, DateOnly asOfDate, CancellationToken cancellationToken = default);

    /// <summary>Runs Accrue then Post for every Approved account whose dates are due, as of <paramref name="asOfDate"/> (defaults to today).</summary>
    Task RunDueAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default);
}
