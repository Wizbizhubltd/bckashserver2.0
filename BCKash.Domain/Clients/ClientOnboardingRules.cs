namespace BCKash.Domain.Clients;

/// <summary>Pure rules for onboarding clients through the office portal.</summary>
public static class ClientOnboardingRules
{
    /// <summary>A group is onboarded with at least this many clients.</summary>
    public const int MinimumGroupSize = 3;

    public const string DetailsFromBvn = "bvn";
    public const string DetailsFromClient = "client";

    /// <summary>How long a BVN lookup can be used to onboard a client before it has to be run again.</summary>
    public static readonly TimeSpan VerificationLifetime = TimeSpan.FromHours(24);

    /// <summary>"Ada Chioma Obi" → ("Ada", "Chioma", "Obi"); a single word is taken as the first name.</summary>
    public static (string FirstName, string? MiddleName, string? LastName) SplitFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (string.Empty, null, null),
            1 => (parts[0], null, null),
            2 => (parts[0], null, parts[1]),
            _ => (parts[0], string.Join(' ', parts[1..^1]), parts[^1]),
        };
    }

    public static bool IsValidBvn(string? bvn) => bvn is { Length: 11 } && bvn.All(char.IsDigit);

    /// <summary>
    /// Names compare case- and space-insensitively; phones compare on their last 10 digits, so
    /// 0803… and +234803… match. Blank on either side isn't a mismatch — there's nothing to compare.
    /// </summary>
    public static bool NamesMatch(string? given, string? fromBvn) =>
        string.IsNullOrWhiteSpace(given) || string.IsNullOrWhiteSpace(fromBvn)
        || string.Equals(Normalize(given), Normalize(fromBvn), StringComparison.OrdinalIgnoreCase);

    public static bool PhonesMatch(string? given, string? fromBvn)
    {
        if (string.IsNullOrWhiteSpace(given) || string.IsNullOrWhiteSpace(fromBvn))
        {
            return true;
        }

        static string Last10(string value)
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length > 10 ? digits[^10..] : digits;
        }

        return Last10(given) == Last10(fromBvn);
    }

    private static string Normalize(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
