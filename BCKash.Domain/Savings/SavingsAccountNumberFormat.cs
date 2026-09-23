namespace BCKash.Domain.Savings;

/// <summary>
/// The system-generated savings account number format (BR-SAV-2/FR-SAV-2). The legacy schema
/// never enforced account-number uniqueness or a format at the DB layer — this is a Phase 7
/// engineering decision, exactly mirroring Phase 2's `ClientAccountNumberFormat`: prefix "SV" +
/// an 8-digit zero-padded sequence (e.g. "SV00000001"). Pure formatting/parsing only — no DB
/// access, so the sequence source lives in BCKash.Infrastructure's SavingsAccountService.
/// </summary>
public static class SavingsAccountNumberFormat
{
    public const string Prefix = "SV";
    public const int SequenceDigits = 8;

    public static string Format(long sequence) =>
        $"{Prefix}{sequence.ToString().PadLeft(SequenceDigits, '0')}";

    /// <summary>True if <paramref name="accountNumber"/> matches this format, with the parsed sequence in <paramref name="sequence"/>.</summary>
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
