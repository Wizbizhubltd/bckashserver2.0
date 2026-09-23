using BCKash.Application.Auth;

namespace BCKash.Infrastructure.Auth;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plainTextPassword) => BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

    public bool Verify(string plainTextPassword, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainTextPassword, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Legacy row with a hash BCrypt.Net can't parse (corrupt/unexpected format) — treat as no match.
            return false;
        }
    }
}
