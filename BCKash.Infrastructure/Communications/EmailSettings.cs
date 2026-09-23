namespace BCKash.Infrastructure.Communications;

public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>New in Phase 9 — not in the original appsettings.json scaffold. Set via `dotnet user-secrets set Email:SmtpUsername/SmtpPassword`, same pattern as Jwt:SigningKey.</summary>
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}
