using BCKash.Domain.Communications;

namespace BCKash.Application.Communications;

/// <summary>
/// SMS delivery via a configured, pluggable <see cref="SmsGateway"/> (BR-COM-4/FR-COM-3) —
/// URL/template-based so BCKash can point at whatever provider they choose without a code
/// change, matching the legacy `sms_gateways` table's shape. FRD §18 explicitly leaves the SMS
/// provider unconfirmed, so no vendor SDK is used here.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(SmsGateway gateway, string toPhone, string message, CancellationToken cancellationToken = default);
}
