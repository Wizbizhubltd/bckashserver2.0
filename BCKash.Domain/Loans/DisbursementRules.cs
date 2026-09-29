namespace BCKash.Domain.Loans;

/// <summary>
/// How a loan is paid out. A bank transfer needs the account it goes to — bank, 10-digit account
/// (NUBAN) number and account name — captured when the loan is raised; cash and cheque pickups don't.
/// </summary>
public static class DisbursementRules
{
    public const int AccountNumberLength = 10;

    /// <summary>Null when the payout details are complete for <paramref name="mode"/>, otherwise what's wrong.</summary>
    public static string? Validate(DisbursementMode? mode, string? bankName, string? accountNumber, string? accountName)
    {
        if (mode is null)
        {
            return "Choose how the loan will be disbursed: cash pickup, cheque pickup or bank transfer.";
        }

        if (mode != DisbursementMode.BankTransfer)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(accountName))
        {
            return "A bank transfer needs the client's bank name, account number and account name.";
        }

        var number = accountNumber?.Trim() ?? string.Empty;
        return number.Length == AccountNumberLength && number.All(char.IsDigit)
            ? null
            : $"The bank account number must be {AccountNumberLength} digits.";
    }
}
