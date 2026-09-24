using System.Text.Json.Serialization;

namespace BCKash.Api.Contracts;

public record LoginRequest(string Email, string Password);

public record TwoFactorRequest(string ChallengeToken, string Code);

public record OtpVerifyRequest(string ChallengeToken, string Code);

public record RefreshRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ForgotPasswordResponse(string ChallengeToken);

public record ResetPasswordRequest(string ChallengeToken, string Code, string NewPassword);

public record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);

// ChallengeType lets a client tell the two challenge kinds apart — both responses would
// otherwise serialize to the exact same {"challengeToken":"..."} shape, leaving the client no
// way to know whether to collect an authenticator-app code (POST /auth/login/2fa) or an
// emailed/texted OTP (POST /auth/login/otp/verify).
public record TwoFactorChallengeResponse(string ChallengeToken, string ChallengeType = "totp");

public record OtpChallengeResponse(string ChallengeToken, string ChallengeType = "otp");

public record UserDataResponse(
    string UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    [property: JsonPropertyName("user_class")] string? UserClass,
    [property: JsonPropertyName("user_type")] string? UserType);

public record OtpVerifyResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, UserDataResponse UserData);
