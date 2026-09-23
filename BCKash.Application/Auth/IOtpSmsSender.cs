namespace BCKash.Application.Auth;

/// <summary>
/// SMS delivery for login OTP codes via the configured system-level provider (Termii) — distinct
/// from <see cref="BCKash.Application.Communications.ISmsSender"/>, which sends through a
/// per-office <c>SmsGateway</c> configured for client campaigns.
/// </summary>
public interface IOtpSmsSender
{
    Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default);
}
