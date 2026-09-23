namespace BCKash.Application.Savings;

public enum SavingsTransferOutcome
{
    Success,
    SavingsNotFound,
    LoanNotFound,
    InvalidAmount,
    SavingsAccountNotTransactable,
    InsufficientBalance,
    InvalidLoanStatus,
}

public record SavingsTransferResult(SavingsTransferOutcome Outcome, decimal? Overpayment = null);

/// <summary>
/// FR-SAV-6: linked loan↔savings transfers. Each method is a single atomic operation (one
/// SaveChangesAsync covering both the savings and loan sides) with one balanced GL batch — see
/// docs/savings-interest-spec.md.
/// </summary>
public interface ISavingsTransferService
{
    /// <summary>Repays a loan funded from a savings account's balance — no cash moves, so this posts Debit SavingsControl / Credit the loan's normal repayment credit lines, instead of the usual Debit FundSource.</summary>
    Task<SavingsTransferResult> RepayLoanFromSavingsAsync(int savingsId, int loanId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Deposits loan-disbursed funds into a savings account instead of an external payout — Debit the loan product's fund source / Credit SavingsControl, the same shape as an ordinary deposit.</summary>
    Task<SavingsTransferResult> DisburseLoanToSavingsAsync(int loanId, int savingsId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default);
}
