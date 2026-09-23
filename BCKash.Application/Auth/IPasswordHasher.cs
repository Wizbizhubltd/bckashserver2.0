namespace BCKash.Application.Auth;

/// <summary>
/// Verifies against the legacy Cartalyst-Sentinel BCrypt hashes stored in
/// `users.password` and hashes new/changed passwords the same way — BCrypt already
/// satisfies NFR-4, so no forced reset or re-hash migration is needed (see Phase 0 plan).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainTextPassword);
    bool Verify(string plainTextPassword, string hash);
}
