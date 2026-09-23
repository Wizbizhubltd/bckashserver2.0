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
}
