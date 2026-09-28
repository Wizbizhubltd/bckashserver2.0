using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum ClientCodeRequestOutcome
{
    /// <summary>The setting is off — the application can be submitted without a code.</summary>
    NotRequired,
    Sent,
    ClientNotFound,
    ProductNotFound,

    /// <summary>The client has neither a mobile number nor an email on file.</summary>
    NoContact,

    /// <summary>A code was sent moments ago — wait before asking for another.</summary>
    TooSoon,

    /// <summary>Too many codes for this client within the hour.</summary>
    TooMany,
}

public record ClientCodeRequestResult(ClientCodeRequestOutcome Outcome, int? CodeId = null, string? SentTo = null, DateTime? ExpiresAtUtc = null, int? RetryAfterSeconds = null);

/// <summary>What staff submit with an application to prove the client agreed to it.</summary>
public record ClientCodeSubmission(int? CodeId, string? Code);

public enum ClientCodeCheckOutcome
{
    Valid,

    /// <summary>No code was needed (setting off, or not an individual client's application).</summary>
    NotRequired,

    Missing,

    /// <summary>The code was sent for a different client, product, amount or staff member.</summary>
    Mismatch,

    /// <summary>Expired, already used, or out of attempts.</summary>
    Expired,

    Incorrect,
}

public record ClientCodeCheckResult(ClientCodeCheckOutcome Outcome, LoanApplicationClientCode? Code = null, int AttemptsLeft = 0);

/// <summary>
/// Client confirmation codes for loans raised on a client's behalf (Settings → Notifications →
/// "Loan raised"). When the setting is on, an individual client's loan application can only be
/// created with a valid code the client received for that exact client, product, amount and staff member.
/// </summary>
public interface ILoanApplicationClientCodeService
{
    public const string SettingKey = "loan_raised_client_code";
    public const int CodeLifetimeMinutes = 10;
    public const int MaxAttempts = 5;
    public const int ResendAfterSeconds = 60;
    public const int MaxCodesPerHour = 5;

    Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default);

    Task<ClientCodeRequestResult> RequestAsync(int clientId, int loanProductId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks <paramref name="submission"/> against <paramref name="application"/> without using it up.
    /// A wrong code counts as an attempt (saved immediately).
    /// </summary>
    Task<ClientCodeCheckResult> CheckAsync(LoanApplication application, ClientCodeSubmission? submission, CancellationToken cancellationToken = default);
}
