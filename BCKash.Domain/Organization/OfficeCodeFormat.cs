namespace BCKash.Domain.Organization;

/// <summary>
/// An office's public identifier: "BCK" + 8 random digits, grouped as BCK453-324-61. Random
/// rather than sequential so codes don't reveal how many offices exist; uniqueness is checked
/// against the database by the caller.
/// </summary>
public static class OfficeCodeFormat
{
    public const string Prefix = "BCK";
    public const int DigitCount = 8;

    /// <param name="nextDigit">Returns a random digit 0–9 on each call.</param>
    public static string Generate(Func<int> nextDigit)
    {
        var digits = new char[DigitCount];
        for (var i = 0; i < DigitCount; i++)
        {
            digits[i] = (char)('0' + nextDigit());
        }

        var d = new string(digits);
        return $"{Prefix}{d[..3]}-{d[3..6]}-{d[6..]}";
    }
}
