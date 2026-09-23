using BCKash.Application.Auth;

namespace BCKash.Infrastructure.Communications;

public record SentOtpSms(string ToPhone, string Message);

/// <summary>Test-only fake — see RecordingEmailSender's doc comment for why this exists and when it's registered.</summary>
public class RecordingOtpSmsSender : IOtpSmsSender
{
    public List<SentOtpSms> Sent { get; } = [];

    public Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentOtpSms(toPhone, message));
        return Task.CompletedTask;
    }
}
