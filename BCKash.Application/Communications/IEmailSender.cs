namespace BCKash.Application.Communications;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

/// <summary>
/// Email delivery (BR-COM-4/FR-COM-1's email campaigns, FR-RPT-2's scheduled report delivery).
/// The real implementation is plain SMTP against the configured relay (`Email:SmtpHost`/
/// `SmtpPort`/`FromAddress` — already pointed at Brevo's relay, credentials supplied via
/// `dotnet user-secrets`, same pattern as `Jwt:SigningKey`) — FRD §18 explicitly leaves the
/// email provider unconfirmed, so this deliberately isn't tied to any vendor SDK.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string body, IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default);
}
