using System.Text.Json;

namespace BCKash.Application.Rbac;

/// <summary>
/// One-time parser for the legacy free-text `users.permissions` / `roles.permissions`
/// columns (FRD §1.5). The real serialization format is unknown — the source dump
/// has no data rows to inspect — so this tries the most likely formats in order and
/// reports failures instead of throwing, so a migration run can log unparseable rows
/// for manual follow-up rather than aborting.
/// </summary>
public static class LegacyPermissionsParser
{
    public record ParseResult(IReadOnlyList<string> Slugs, bool WasParsed);

    public static ParseResult Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ParseResult([], true);
        }

        // Try 1: JSON array of slugs, e.g. ["users.create","loans.view"].
        try
        {
            var slugs = JsonSerializer.Deserialize<string[]>(raw);
            if (slugs is not null)
            {
                return new ParseResult(Clean(slugs), true);
            }
        }
        catch (JsonException)
        {
            // fall through to the next format
        }

        // Try 2: comma-separated slugs, e.g. "users.create,loans.view".
        if (raw.Contains(','))
        {
            var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                return new ParseResult(Clean(parts), true);
            }
        }

        // Try 3: a single bare slug.
        if (!raw.Contains('{') && !raw.Contains('['))
        {
            return new ParseResult(Clean([raw]), true);
        }

        return new ParseResult([], false);
    }

    private static IReadOnlyList<string> Clean(IEnumerable<string> slugs) =>
        slugs.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
