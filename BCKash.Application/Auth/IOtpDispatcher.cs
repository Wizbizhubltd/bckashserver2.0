namespace BCKash.Application.Auth;

/// <summary>A one-time code to deliver by email and/or SMS — whichever the recipient has on file.</summary>
public record OtpMessage(string? Email, string? Phone, string Subject, string Body);

/// <summary>
/// Hands OTP delivery off so the requesting endpoint doesn't wait on SMTP/SMS. A slow or
/// unreachable relay was holding login and forgot-password requests open for over a minute —
/// long past the portal's request timeout — even though the code was already saved and valid.
/// </summary>
public interface IOtpDispatcher
{
    Task DispatchAsync(OtpMessage message, CancellationToken cancellationToken = default);
}
