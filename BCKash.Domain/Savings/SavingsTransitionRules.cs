namespace BCKash.Domain.Savings;

/// <summary>Savings-account-level transitions (BR-SAV-2/FR-SAV-2) — mirrors LoanTransitionRules' shape.</summary>
public static class SavingsTransitionRules
{
    public static bool CanApprove(SavingsAccountStatus current) => current == SavingsAccountStatus.Pending;

    public static bool CanDecline(SavingsAccountStatus current) => current == SavingsAccountStatus.Pending;

    public static bool CanClose(SavingsAccountStatus current) => current == SavingsAccountStatus.Approved;

    public static bool CanWithdrawAccount(SavingsAccountStatus current) => current == SavingsAccountStatus.Approved;

    /// <summary>Whether the account can currently receive deposits/withdrawals/charges/interest.</summary>
    public static bool CanTransact(SavingsAccountStatus current) => current == SavingsAccountStatus.Approved;
}
