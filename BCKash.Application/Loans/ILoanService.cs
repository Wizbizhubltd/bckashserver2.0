using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanWriteOutcome
{
    Success,
    NotFound,
    InvalidTransition,
    ReasonRequired,

    /// <summary>"Loans draw on office funds" is on and the office's balance can't cover the disbursement — see Error.</summary>
    InsufficientOfficeFunds,
}

public record LoanWriteResult(LoanWriteOutcome Outcome, Loan? Loan = null, string? Error = null);

/// <summary>
/// FR-LN-7's need-changes/pending cycle, plus FR-LN-8's bare disbursement transition — see
/// LoanTransitionRules for why nothing beyond that (repayments, write-off, etc.) is exposed here.
/// </summary>
public interface ILoanService
{
    Task<LoanWriteResult> RequestChangesAsync(int id, string notes, CancellationToken cancellationToken = default);

    Task<LoanWriteResult> ResubmitAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-LN-8: records the disbursement itself (date, actual disbursed amount, notes) and moves
    /// the loan to Disbursed. Does NOT generate a repayment schedule — that needs the
    /// still-unresolved FR-LN-15 interest/amortization spec; see LoanService's implementation.
    /// </summary>
    Task<LoanWriteResult> DisburseAsync(int id, DateOnly? disbursementDate, decimal disbursedAmount, string? notes, CancellationToken cancellationToken = default);
}
