namespace BCKash.Application.Loans;

/// <summary>
/// Closes a loan out once nothing is owed on it — principal, interest, fees and penalties all paid, waived or
/// written off — and reopens one it closed if a reversal leaves money owed again.
/// </summary>
public interface ILoanCompletionService
{
    /// <summary>
    /// Marks the loan Closed ("Completed") if it's being repaid and its whole schedule is settled. True if it
    /// was just closed.
    /// </summary>
    Task<bool> CompleteIfSettledAsync(int loanId, CancellationToken cancellationToken = default);

    /// <summary>Reopens a loan this service closed if money is owed on it again (after a repayment was reversed).</summary>
    Task ReopenIfOwingAsync(int loanId, CancellationToken cancellationToken = default);

    /// <summary>Closes every loan being repaid whose schedule is already settled; returns how many.</summary>
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}
