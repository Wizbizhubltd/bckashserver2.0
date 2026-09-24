namespace BCKash.Application.Auth;

public enum LoginOutcomeType
{
    Success,
    RequiresTwoFactor,

    /// <summary>Password verified and the user has no Google2FA enabled — a login OTP has been generated and sent to their email and phone; call VerifyLoginOtpAsync to finish.</summary>
    RequiresOtp,
    InvalidCredentials,
    Blocked,
    LockedOut,
    OutsideAccessWindow,

    /// <summary>New in the staff-onboarding pass — a Pending (not-yet-authorized) staff record can't log in until an Authorizer of the same user_type approves it.</summary>
    PendingOnboarding,
}

public record LoginResult(
    LoginOutcomeType Outcome,
    AccessToken? AccessToken = null,
    string? RefreshToken = null,
    string? TwoFactorChallengeToken = null,
    string? OtpChallengeToken = null);

public enum TwoFactorOutcomeType
{
    Success,
    InvalidChallenge,
    InvalidCode,
}

public record TwoFactorResult(TwoFactorOutcomeType Outcome, AccessToken? AccessToken = null, string? RefreshToken = null);

public enum OtpVerifyOutcomeType
{
    Success,
    InvalidChallenge,
    InvalidCode,
    TooManyAttempts,
}

/// <summary>The user's identity summary returned alongside tokens once their login OTP is verified.</summary>
public record UserLoginData(string UserId, string FullName, string Email, string? PhoneNumber, string? UserClass, string? UserType);

public record OtpVerifyResult(
    OtpVerifyOutcomeType Outcome,
    AccessToken? AccessToken = null,
    string? RefreshToken = null,
    UserLoginData? UserData = null);

public enum RefreshOutcomeType
{
    Success,
    InvalidOrExpired,
}

public enum PasswordResetRequestOutcomeType
{
    /// <summary>
    /// Returned whether or not the email matched an eligible account, so the endpoint can't be
    /// used to discover which emails are registered — an unknown email just gets a challenge that
    /// will never verify.
    /// </summary>
    Accepted,
    LockedOut,

    /// <summary>A reset code was already sent to this account within the cooldown window.</summary>
    TooSoon,
}

public record PasswordResetRequestResult(PasswordResetRequestOutcomeType Outcome, string? ChallengeToken = null);

public enum PasswordResetOutcomeType
{
    Success,
    InvalidChallenge,
    InvalidCode,
    TooManyAttempts,
    WeakPassword,
}

public record PasswordResetResult(PasswordResetOutcomeType Outcome);

public record RefreshResult(RefreshOutcomeType Outcome, AccessToken? AccessToken = null, string? RefreshToken = null);

/// <summary>
/// Orchestrates the full login flow: throttling (FR-SEC-2), access-window check,
/// password verification (NFR-4), 2FA challenge (FR-SEC-3), and refresh-token
/// rotation. Implemented in BCKash.Infrastructure against BCKashDbContext.
/// </summary>
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, string? ip, CancellationToken cancellationToken = default);

    Task<TwoFactorResult> VerifyTwoFactorAsync(string challengeToken, string totpCode, CancellationToken cancellationToken = default);

    Task<OtpVerifyResult> VerifyLoginOtpAsync(string challengeToken, string code, CancellationToken cancellationToken = default);

    /// <summary>Self-service forgot-password step 1: emails/texts a reset OTP to the account, if one exists and may sign in.</summary>
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string? ip, CancellationToken cancellationToken = default);

    /// <summary>Forgot-password step 2: verifies the reset OTP, sets the new password and signs the user out everywhere.</summary>
    Task<PasswordResetResult> ResetPasswordAsync(string challengeToken, string code, string newPassword, CancellationToken cancellationToken = default);

    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
