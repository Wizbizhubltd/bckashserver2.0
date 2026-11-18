namespace BCKash.Application.Auth;

/// <summary>A one-time code to deliver by email and/or SMS — whichever the recipient has on file.</summary>
/// <remarks>
/// <paramref name="Bulk"/> marks a message nobody is waiting on (the daily customer reminders): those go
/// through their own lane, so thousands of reminders can never hold up a staff member's login code.
/// </remarks>
public record OtpMessage(string? Email, string? Phone, string Subject, string Body, bool Bulk = false);

/// <summary>
/// Hands OTP delivery off so the requesting endpoint doesn't wait on SMTP/SMS. A slow or
/// unreachable relay was holding login and forgot-password requests open for over a minute —
/// long past the portal's request timeout — even though the code was already saved and valid.
/// </summary>
public interface IOtpDispatcher
{
    Task DispatchAsync(OtpMessage message, CancellationToken cancellationToken = default);
}
