using BCKash.Application.Communications;

namespace BCKash.Infrastructure.Communications;

public record SentEmail(string ToAddress, string Subject, string Body, IReadOnlyList<EmailAttachment>? Attachments);

/// <summary>
/// Test-only fake, registered instead of <see cref="SmtpEmailSender"/> when `Testing:UseSqlite`
/// is set (same config flag that already switches the DB provider for the integration-test
/// host) — no test environment can actually deliver SMTP mail, and this lets tests assert what
/// would have been sent. Registered as a singleton so a test can resolve it from the shared
/// <c>WebApplicationFactory</c> and inspect <see cref="Sent"/> after exercising the API.
/// </summary>
public class RecordingEmailSender : IEmailSender
{
    public List<SentEmail> Sent { get; } = [];

    public Task SendAsync(string toAddress, string subject, string body, IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentEmail(toAddress, subject, body, attachments));
        return Task.CompletedTask;
    }
}
