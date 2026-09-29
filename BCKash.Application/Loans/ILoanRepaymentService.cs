using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanRepaymentWriteOutcome
{
    Success,
    NotFound,
    InvalidLoanStatus,
    InvalidAmount,
    TransactionNotFound,
    AlreadyReversed,
    NotReversible,

    /// <summary>More than the client still owes on the loan — see <see cref="ILoanRepaymentService.RemainingAsync"/>.</summary>
    ExceedsBalance,
}

public record LoanRepaymentWriteResult(LoanRepaymentWriteOutcome Outcome, LoanTransaction? Transaction = null, decimal Overpayment = 0m);

/// <summary>
/// FR-LN-16 (repayment recording/allocation) and FR-LN-18 (reversal). See
/// LoanRepaymentAllocationEngine for the allocation algorithm and LoanRepaymentService's doc
/// comments for the reversal and overpayment scope boundaries.
/// </summary>
public interface ILoanRepaymentService
{
    Task<LoanRepaymentWriteResult> RecordRepaymentAsync(int loanId, decimal amount, int? paymentTypeId, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    Task<LoanRepaymentWriteResult> ReverseAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most a repayment can be: everything still owed on the schedule — grossed up on a savings loan, since
    /// a share of each payment goes to the client's savings. Zero once the loan is fully repaid; null if there's no such loan.
    /// </summary>
    Task<decimal?> RemainingAsync(int loanId, CancellationToken cancellationToken = default);
}
