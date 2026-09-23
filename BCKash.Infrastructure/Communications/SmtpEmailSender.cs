using BCKash.Application.Communications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BCKash.Infrastructure.Communications;

/// <summary>Plain SMTP delivery against the configured relay — see EmailSettings' doc comment.</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string toAddress, string subject, string body, IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };
        foreach (var attachment in attachments ?? [])
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        // Revocation checking off: .NET's OCSP/CRL lookup for Brevo's cert chain has been
        // observed to fail with "incomplete certificate revocation check" on this network even
        // though the certificate itself is valid — MailKit then aborts the handshake entirely.
        // This only skips the revocation-status check, not certificate/hostname validation.
        client.CheckCertificateRevocation = false;
        // Auto (not a hardcoded StartTls) — port 465 (implicit TLS) and 587 (STARTTLS) need
        // different handshakes, and this relay has been used on both across environments.
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(_settings.SmtpUsername))
        {
            await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
