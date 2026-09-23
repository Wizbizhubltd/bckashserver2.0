namespace BCKash.Application.Loans;

public enum LoanNpaOutcome
{
    Success,
    NotFound,
}

public record LoanNpaResult(LoanNpaOutcome Outcome, bool IsNpa = false, bool IncomeSuspended = false, int DaysInArrears = 0);

/// <summary>
/// FR-LN-25: NPA classification and income suspension, computed on demand (no job scheduler
/// exists anywhere in this codebase to run this periodically) — called after every repayment/
/// reversal/write-off, and exposed as its own endpoint for a manual recompute.
/// </summary>
public interface ILoanNpaService
{
    Task<LoanNpaResult> RecomputeAsync(int loanId, CancellationToken cancellationToken = default);
}
