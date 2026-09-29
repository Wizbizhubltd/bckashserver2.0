using BCKash.Domain.Clients;

namespace BCKash.Application.Clients;

/// <summary>
/// What a client has saved from their loan repayments, and what they'd get if they withdrew now: all of it
/// once no loan is running, or less the early cash-out charge (<c>EarlyWithdrawalFeeRate</c>, a setting) while one is.
/// </summary>
public record ClientSavingsSummary(
    decimal Balance,
    bool HasRunningLoan,
    decimal EarlyWithdrawalFeeRate,
    decimal WithdrawalFee,
    decimal WithdrawalPayout,
    IReadOnlyList<ClientSavingsEntry> Entries);

public enum ClientSavingsWithdrawalOutcome
{
    Success,
    NotFound,
    NothingToWithdraw,
}

public record ClientSavingsWithdrawalResult(ClientSavingsWithdrawalOutcome Outcome, decimal Payout = 0, decimal Fee = 0, ClientSavingsSummary? Summary = null);

/// <summary>A client's loan savings — see <see cref="ClientSavingsRules"/>.</summary>
public interface IClientSavingsService
{
    Task<ClientSavingsSummary> GetAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Pays out the whole balance: in full, or less the early cash-out fee while a loan is still running.</summary>
    Task<ClientSavingsWithdrawalResult> WithdrawAsync(int clientId, string? notes, CancellationToken cancellationToken = default);
}
