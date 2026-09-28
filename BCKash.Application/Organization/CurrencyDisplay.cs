using System.Globalization;

namespace BCKash.Application.Organization;

/// <summary>
/// How money is shown everywhere — both portals and the SMS/email the system sends (Settings →
/// Organisation → "Currency display").
/// </summary>
public record CurrencyDisplay(string Symbol, CurrencySymbolPosition Position)
{
    public static readonly CurrencyDisplay Default = new("₦", CurrencySymbolPosition.Left);

    /// <summary>E.g. "₦5,000" / "5,000₦"; a multi-letter symbol gets a space ("NGN 5,000"). Whole amounts drop the decimals.</summary>
    public string Format(decimal amount)
    {
        var number = Math.Abs(amount).ToString(amount % 1 == 0 ? "#,##0" : "#,##0.00", CultureInfo.InvariantCulture);
        var gap = Symbol.Length > 1 ? " " : string.Empty;
        var text = Position == CurrencySymbolPosition.Left ? $"{Symbol}{gap}{number}" : $"{number}{gap}{Symbol}";
        return amount < 0 ? "-" + text : text;
    }
}

public enum CurrencySymbolPosition
{
    Left,
    Right,
}

public static class CurrencyDisplayKeys
{
    public const string Symbol = "currency_symbol";
    public const string Position = "currency_position";
    public const int MaxSymbolLength = 5;

    public static readonly string[] All = [Symbol, Position];

    /// <summary>Null when <paramref name="value"/> is acceptable for <paramref name="key"/> (always null for other keys).</summary>
    public static string? Validate(string key, string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return key switch
        {
            Symbol when trimmed.Length == 0 => "Enter a currency symbol, e.g. ₦.",
            Symbol when trimmed.Length > MaxSymbolLength => $"The currency symbol must be {MaxSymbolLength} characters or fewer.",
            Position when trimmed is not ("left" or "right") => "The symbol position must be before (left) or after (right) the amount.",
            _ => null,
        };
    }
}

public interface ICurrencyDisplayProvider
{
    /// <summary>Invalid or missing settings fall back to <see cref="CurrencyDisplay.Default"/> (₦, before the amount).</summary>
    Task<CurrencyDisplay> GetAsync(CancellationToken cancellationToken = default);
}
