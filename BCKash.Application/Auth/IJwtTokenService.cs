using BCKash.Domain.Identity;

namespace BCKash.Application.Auth;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken GenerateAccessToken(User user, IReadOnlyCollection<string> permissionSlugs);

    /// <summary>
    /// Short-lived token issued after password verification for a user with
    /// `enable_google2fa = true`, proving the client already passed the first
    /// factor without the server holding any session state between steps.
    /// </summary>
    string GenerateTwoFactorChallengeToken(int userId);

    /// <summary>Returns the user id if <paramref name="challengeToken"/> is a valid, unexpired 2FA challenge token.</summary>
    int? ValidateTwoFactorChallengeToken(string challengeToken);

    /// <summary>
    /// Short-lived token issued after password verification for a user without Google2FA
    /// enabled, once their login OTP has been generated and sent. Carries only the
    /// <see cref="Domain.Identity.LoginOtp"/> row's id — never the code or its hash — so
    /// intercepting the token alone can't be brute-forced offline.
    /// </summary>
    string GenerateLoginOtpChallengeToken(int userId, int otpId);

    /// <summary>Returns the (user id, OTP id) if <paramref name="challengeToken"/> is a valid, unexpired login-OTP challenge token.</summary>
    (int UserId, int OtpId)? ValidateLoginOtpChallengeToken(string challengeToken);
}
