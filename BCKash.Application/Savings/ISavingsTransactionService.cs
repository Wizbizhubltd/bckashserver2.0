using BCKash.Domain.Savings;

namespace BCKash.Application.Savings;

public enum SavingsTransactionWriteOutcome
{
    Success,
    NotFound,
    InvalidAccountStatus,
    InvalidAmount,

    /// <summary>FR-SAV-3: the withdrawal would take the balance below its floor (minimum balance, or negative past the overdraft limit).</summary>
    InsufficientBalance,

    TransactionNotFound,
    AlreadyReversed,
    NotReversible,
}

public record SavingsTransactionWriteResult(SavingsTransactionWriteOutcome Outcome, SavingsTransaction? Transaction = null);

/// <summary>
/// Deposit/withdrawal recording and reversal (BR-SAV-3/FR-SAV-3). Unlike LoanTransaction,
/// the legacy `savings_transactions` table has no `payment_type_id` column of its own — only
/// `payment_detail_id` (an out-of-scope FK, same "kept as a plain scalar column" treatment as
/// everywhere else in this codebase), so there's nowhere to capture a payment type directly.
/// </summary>
public interface ISavingsTransactionService
{
    Task<SavingsTransactionWriteResult> RecordDepositAsync(int accountId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    Task<SavingsTransactionWriteResult> RecordWithdrawalAsync(int accountId, decimal amount, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    Task<SavingsTransactionWriteResult> ReverseAsync(int transactionId, CancellationToken cancellationToken = default);
}
