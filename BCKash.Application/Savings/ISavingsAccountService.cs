using BCKash.Domain.Savings;

namespace BCKash.Application.Savings;

public enum SavingsAccountWriteOutcome
{
    Success,
    NotFound,
    ProductNotFound,
    InvalidTransition,
    AccountNumberGenerationFailed,

    /// <summary>Close blocked — the account still carries a non-zero balance; withdraw it first.</summary>
    NonZeroBalance,

    ReasonRequired,
}

public record SavingsAccountWriteResult(SavingsAccountWriteOutcome Outcome, SavingsAccount? Account = null);

/// <summary>Savings account lifecycle (BR-SAV-2/FR-SAV-2).</summary>
public interface ISavingsAccountService
{
    Task<SavingsAccountWriteResult> CreateAsync(SavingsAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="overdraftLimit"/> has no product-level default to copy — unlike every
    /// other copied-at-approval field, `SavingsProduct` has no `OverdraftLimit` column at all
    /// (only `SavingsAccount` does, per the legacy schema — overdraft limits are account-
    /// specific even when the product enables overdraft), so it's set explicitly here.
    /// </summary>
    Task<SavingsAccountWriteResult> ApproveAsync(int id, decimal? openingBalance, decimal? overdraftLimit, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    Task<SavingsAccountWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<SavingsAccountWriteResult> CloseAsync(int id, string? notes, CancellationToken cancellationToken = default);

    Task<SavingsAccountWriteResult> WithdrawAccountAsync(int id, string? notes, CancellationToken cancellationToken = default);
}
