namespace BCKash.SharedKernel;

/// <summary>
/// Every phone number BCKash stores is in Nigerian international format: <c>+234</c> followed by
/// the subscriber number with the local leading 0 dropped (0803 123 4567 → +2348031234567),
/// whatever format it was typed in.
/// </summary>
public static class PhoneNumbers
{
    public const string NigeriaDialCode = "+234";

    /// <summary>
    /// Normalizes <paramref name="value"/> to <c>+234XXXXXXXXXX</c>. Accepts local (0803…),
    /// international (+234803…, 234803…, +2340803…) and bare subscriber (803…) forms, ignoring
    /// spaces, dashes and brackets. Blank values, and values with no digits, are returned as-is.
    /// </summary>
    public static string? ToNigerianInternational(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return value.Trim();
        }

        // A subscriber number is 10 digits, so a longer one starting 234 carries the dial code.
        if (digits.Length > 10 && digits.StartsWith("234", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }

        if (digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        return NigeriaDialCode + digits;
    }
}
