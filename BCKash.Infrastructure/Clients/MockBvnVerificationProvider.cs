using BCKash.Application.Clients;

namespace BCKash.Infrastructure.Clients;

public enum MockBvnMode
{
    /// <summary>Every lookup comes back matching exactly what was submitted.</summary>
    Match,

    /// <summary>Every lookup comes back with random details that differ from what was submitted.</summary>
    Conflicting,
}

/// <summary>
/// Stands in for the BVN gateway when <c>UseMockBvn</c> is set, so onboarding can be tried without
/// real BVNs: <c>isAMatch</c> returns the submitted details, <c>isConflicting</c> returns random ones
/// that differ from them. Every BVN is treated as valid. Leave <c>UseMockBvn</c> unset in production.
/// </summary>
public class MockBvnVerificationProvider : IBvnVerificationProvider
{
    public const string ConfigKey = "UseMockBvn";
    public const string MatchValue = "isAMatch";
    public const string ConflictingValue = "isConflicting";

    private static readonly string[] FirstNames = ["CHIDINMA", "OLUWASEUN", "IBRAHIM", "NGOZI", "ADEBOLA", "EMEKA", "FATIMA", "TUNDE", "AMAKA", "YUSUF"];
    private static readonly string[] MiddleNames = ["ADAEZE", "OLAMIDE", "KELECHI", "BUKOLA", "CHUKWUMA", "HAUWA"];
    private static readonly string[] LastNames = ["OKAFOR", "ADEYEMI", "BELLO", "NWOSU", "OGUNLEYE", "ABUBAKAR", "EZE", "BALOGUN", "OKONKWO", "LAWAL"];
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private readonly MockBvnMode _mode;

    public MockBvnVerificationProvider(MockBvnMode mode)
    {
        _mode = mode;
    }

    /// <summary>
    /// Null when <paramref name="value"/> is unset or empty (use the real gateway). Any value other
    /// than <see cref="MatchValue"/> or <see cref="ConflictingValue"/> stops the API from starting,
    /// so a typo can't quietly send lookups to the real gateway.
    /// </summary>
    public static MockBvnMode? ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "isamatch" => MockBvnMode.Match,
            "isconflicting" => MockBvnMode.Conflicting,
            _ => throw new InvalidOperationException(
                $"{ConfigKey} is '{value}'. Set it to '{MatchValue}' or '{ConflictingValue}', or leave it unset to verify BVNs with the gateway."),
        };
    }

    public Task<BvnLookupResult> LookupAsync(string bvn, string firstName, string? middleName, string lastName, string? phone, CancellationToken cancellationToken = default)
    {
        if (_mode == MockBvnMode.Match)
        {
            // Upper-cased like real BVN records; names compare case-insensitively, so it still matches.
            return Task.FromResult(new BvnLookupResult(
                BvnLookupOutcome.Found,
                FirstName: firstName.ToUpperInvariant(),
                MiddleName: middleName?.ToUpperInvariant(),
                LastName: lastName.ToUpperInvariant(),
                Phone: phone,
                BirthDate: RandomBirthDate()));
        }

        return Task.FromResult(new BvnLookupResult(
            BvnLookupOutcome.Found,
            FirstName: PickOtherThan(FirstNames, firstName),
            MiddleName: middleName is null ? null : PickOtherThan(MiddleNames, middleName),
            LastName: PickOtherThan(LastNames, lastName),
            Phone: RandomPhoneOtherThan(phone),
            BirthDate: RandomBirthDate()));
    }

    private static string PickOtherThan(string[] options, string submitted)
    {
        var others = options.Where(o => !string.Equals(o, submitted.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return others[Random.Shared.Next(others.Length)];
    }

    private static string RandomPhoneOtherThan(string? submitted)
    {
        var submittedDigits = new string((submitted ?? string.Empty).Where(char.IsDigit).ToArray());
        while (true)
        {
            var phone = $"0{new[] { "803", "806", "813", "816", "703", "706", "810", "905" }[Random.Shared.Next(8)]}{Random.Shared.Next(0, 10_000_000):D7}";
            if (!submittedDigits.EndsWith(phone[1..], StringComparison.Ordinal))
            {
                return phone;
            }
        }
    }

    private static string RandomBirthDate() =>
        $"{Random.Shared.Next(1, 29):D2}-{Months[Random.Shared.Next(12)]}-{Random.Shared.Next(60, 100):D2}";
}
