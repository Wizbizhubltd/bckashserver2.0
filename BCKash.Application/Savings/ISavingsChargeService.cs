using BCKash.Domain.Savings;

namespace BCKash.Application.Savings;

public enum SavingsChargeWriteOutcome
{
    Success,
    NotFound,
    ChargeNotFound,
    InvalidAmount,
    AlreadyWaived,
    AlreadyPaid,
    InsufficientBalance,
}

public record SavingsChargeWriteResult(SavingsChargeWriteOutcome Outcome, SavingsCharge? Charge = null);

/// <summary>
/// Savings-specific charges (BR-SAV-5/FR-SAV-5) — activation/withdrawal/annual/monthly/
/// specified-due-date fees. Amounts are entered directly rather than computed from a
/// percentage/frequency rule, the same simplification Phase 4 applied to loan charges.
/// </summary>
public interface ISavingsChargeService
{
    Task<SavingsChargeWriteResult> AttachAsync(int accountId, SavingsChargeType chargeType, bool penalty, decimal amount, DateOnly? dueDate, CancellationToken cancellationToken = default);

    Task<SavingsChargeWriteResult> WaiveAsync(int savingsChargeId, CancellationToken cancellationToken = default);

    Task<SavingsChargeWriteResult> PayDueAsync(int savingsChargeId, DateOnly? date, CancellationToken cancellationToken = default);
}
