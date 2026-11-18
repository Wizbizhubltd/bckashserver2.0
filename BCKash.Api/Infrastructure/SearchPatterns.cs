namespace BCKash.Api.Infrastructure;

/// <summary>
/// LIKE patterns for search boxes. Prefer <see cref="StartsWith"/>: on an indexed column
/// <c>LIKE 'x%'</c> is an index range lookup, while <c>LIKE '%x%'</c> reads every row — fine on a small
/// table, a bottleneck on clients/loans/transactions with many staff searching at once.
/// </summary>
public static class SearchPatterns
{
    /// <summary>The escape character to pass to <c>EF.Functions.Like(column, pattern, Escape)</c>.</summary>
    public const string Escape = "\\";

    /// <summary>"ab_c" → "ab\_c%": the typed text matched literally, at the start of the value.</summary>
    public static string StartsWith(string text) => $"{Literal(text)}%";

    /// <summary>Makes a typed % or _ match literally instead of acting as a LIKE wildcard.</summary>
    public static string Literal(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
