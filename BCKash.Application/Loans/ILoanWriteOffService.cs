using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum LoanWriteOffOutcome
{
    Success,
    NotFound,
    InvalidTransition,
    ReasonRequired,
    InvalidAmount,
}

public record LoanWriteOffResult(LoanWriteOffOutcome Outcome, Loan? Loan = null, LoanTransaction? Transaction = null);

/// <summary>FR-LN-24: write-off and write-off recovery.</summary>
public interface ILoanWriteOffService
{
    Task<LoanWriteOffResult> WriteOffAsync(int loanId, string reason, DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>Recorded as its own transaction, not mapped back onto schedule lines — the debt is already written off (see LoanWriteOffService's doc comment).</summary>
    Task<LoanWriteOffResult> RecordRecoveryAsync(int loanId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default);
}
