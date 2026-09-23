using BCKash.Application.Communications;
using BCKash.Domain.Communications;

namespace BCKash.Infrastructure.Communications;

public record SentSms(int GatewayId, string ToPhone, string Message);

/// <summary>Test-only fake — see RecordingEmailSender's doc comment for why this exists and when it's registered.</summary>
public class RecordingSmsSender : ISmsSender
{
    public List<SentSms> Sent { get; } = [];

    public Task SendAsync(SmsGateway gateway, string toPhone, string message, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentSms(gateway.Id, toPhone, message));
        return Task.CompletedTask;
    }
}
