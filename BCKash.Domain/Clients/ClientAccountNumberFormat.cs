namespace BCKash.Domain.Clients;

/// <summary>
/// The system-generated client account number format (BR-CLI-6/FR-CLI-1). The legacy schema
/// never enforced account-number uniqueness or a format at the DB layer — this is a Phase 2
/// engineering decision, documented in FRD.md: prefix "CL" + an 8-digit zero-padded sequence
/// (e.g. "CL00000001"). Pure formatting/parsing only — no DB access, so the sequence source
/// (querying existing clients for the current max) lives in BCKash.Infrastructure's ClientService.
/// </summary>
public static class ClientAccountNumberFormat
{
    public const string Prefix = "CL";
    public const int SequenceDigits = 8;

    public static string Format(long sequence) =>
        $"{Prefix}{sequence.ToString().PadLeft(SequenceDigits, '0')}";

    /// <summary>True if <paramref name="accountNo"/> matches this format, with the parsed sequence in <paramref name="sequence"/>.</summary>
    public static bool TryParseSequence(string? accountNo, out long sequence)
    {
        sequence = 0;

        if (accountNo is null || !accountNo.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = accountNo[Prefix.Length..];
        return digits.Length == SequenceDigits && long.TryParse(digits, out sequence);
    }
}
