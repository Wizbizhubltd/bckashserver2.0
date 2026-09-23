namespace BCKash.Domain.Loans;

/// <summary>
/// The system-generated loan account number format — identical shape to
/// <see cref="Clients.ClientAccountNumberFormat"/> (prefix "LN" + an 8-digit zero-padded
/// sequence, e.g. "LN00000001"). <see cref="Loan.AccountNumber"/> already has a unique index
/// from Phase 0 scaffolding but nothing populates it; every Loan created by an application
/// approval needs one (see LoanApplicationService).
/// </summary>
public static class LoanAccountNumberFormat
{
    public const string Prefix = "LN";
    public const int SequenceDigits = 8;

    public static string Format(long sequence) =>
        $"{Prefix}{sequence.ToString().PadLeft(SequenceDigits, '0')}";

    public static bool TryParseSequence(string? accountNumber, out long sequence)
    {
        sequence = 0;

        if (accountNumber is null || !accountNumber.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = accountNumber[Prefix.Length..];
        return digits.Length == SequenceDigits && long.TryParse(digits, out sequence);
    }
}
