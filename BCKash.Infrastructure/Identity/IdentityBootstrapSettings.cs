namespace BCKash.Infrastructure.Identity;

public class IdentityBootstrapSettings
{
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// If set, the seeder creates the first super_admin user with this email on startup
    /// (only when no super_admin exists yet). Left empty by default so a fresh dev/test
    /// database doesn't silently gain an admin nobody configured — set via
    /// `dotnet user-secrets set Bootstrap:SuperAdminEmail "you@yourcompany.com"`.
    /// </summary>
    public string SuperAdminEmail { get; set; } = string.Empty;

    public string SuperAdminFirstName { get; set; } = "System";
    public string SuperAdminLastName { get; set; } = "Administrator";

    /// <summary>
    /// Optional — set so the seeded super admin can actually receive login OTPs by SMS (see
    /// AuthService.LoginAsync). Left empty, the account still works; it just only gets the OTP
    /// by email.
    /// </summary>
    public string? SuperAdminPhone { get; set; }
}
