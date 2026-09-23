namespace BCKash.Domain.Savings;

/// <summary>
/// FR-SAV-3's balance-floor enforcement: a withdrawal (or a fee/charge deduction, or a
/// loan-repayment-from-savings transfer) may not take the balance below the account's floor —
/// zero/minimum-balance normally, or down to the negative overdraft limit if overdraft is
/// enabled.
/// </summary>
public static class SavingsBalanceRules
{
    public static decimal EffectiveFloor(bool allowOverdraft, decimal? minimumBalance, decimal? overdraftLimit) =>
        allowOverdraft ? -(overdraftLimit ?? 0m) : (minimumBalance ?? 0m);

    public static bool CanWithdraw(decimal currentBalance, decimal amount, decimal floor) =>
        currentBalance - amount >= floor;
}
