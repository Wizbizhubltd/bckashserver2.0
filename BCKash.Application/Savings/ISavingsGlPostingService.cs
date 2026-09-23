using BCKash.Domain.Savings;

namespace BCKash.Application.Savings;

/// <summary>
/// GL posting hook for savings transactions (BR-GL-2, FR-GL-2 extended to savings in Phase 7) —
/// mirrors ILoanGlPostingService's exact shape. See docs/savings-interest-spec.md for the
/// SavingsReference/SavingsControl account-role caveat.
/// </summary>
public interface ISavingsGlPostingService
{
    Task PostDepositAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default);

    Task PostWithdrawalAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default);

    Task PostInterestAsync(SavingsAccount account, SavingsTransaction transaction, CancellationToken cancellationToken = default);

    Task PostChargeAsync(SavingsAccount account, SavingsTransaction transaction, bool isPenalty, CancellationToken cancellationToken = default);
}
