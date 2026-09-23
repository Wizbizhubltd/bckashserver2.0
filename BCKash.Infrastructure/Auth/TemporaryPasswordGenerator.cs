using System.Security.Cryptography;

namespace BCKash.Infrastructure.Auth;

/// <summary>Generates a random, one-time password for newly-onboarded staff, emailed to them per the staff-onboarding flow.</summary>
public static class TemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no I/O — avoids visual ambiguity
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%&*";
    private const string All = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = 12)
    {
        Span<char> password = stackalloc char[length];

        // Guarantee at least one of each character class, then fill the rest randomly.
        password[0] = Pick(Upper);
        password[1] = Pick(Lower);
        password[2] = Pick(Digits);
        password[3] = Pick(Symbols);
        for (var i = 4; i < length; i++)
        {
            password[i] = Pick(All);
        }

        // Shuffle so the guaranteed characters aren't always in the first four positions.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
