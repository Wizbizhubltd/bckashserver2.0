using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request.Email, request.Password, ip, cancellationToken);

        return result.Outcome switch
        {
            LoginOutcomeType.Success => Ok(new TokenResponse(result.AccessToken!.Token, result.AccessToken.ExpiresAtUtc, result.RefreshToken!)),
            LoginOutcomeType.RequiresTwoFactor => Ok(new TwoFactorChallengeResponse(result.TwoFactorChallengeToken!)),
            LoginOutcomeType.RequiresOtp => Ok(new OtpChallengeResponse(result.OtpChallengeToken!)),
            LoginOutcomeType.InvalidCredentials => Problem(title: "Invalid credentials", statusCode: StatusCodes.Status401Unauthorized),
            LoginOutcomeType.Blocked => Problem(title: "Account is blocked", statusCode: StatusCodes.Status403Forbidden),
            LoginOutcomeType.LockedOut => Problem(title: "Too many failed attempts — try again later", statusCode: StatusCodes.Status429TooManyRequests),
            LoginOutcomeType.OutsideAccessWindow => Problem(title: "Login is not permitted at this time", statusCode: StatusCodes.Status403Forbidden),
            LoginOutcomeType.PendingOnboarding => Problem(title: "This account is awaiting authorization and cannot log in yet", statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("login/2fa")]
    public async Task<IActionResult> VerifyTwoFactor(TwoFactorRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyTwoFactorAsync(request.ChallengeToken, request.Code, cancellationToken);

        return result.Outcome switch
        {
            TwoFactorOutcomeType.Success => Ok(new TokenResponse(result.AccessToken!.Token, result.AccessToken.ExpiresAtUtc, result.RefreshToken!)),
            TwoFactorOutcomeType.InvalidChallenge => Problem(title: "Invalid or expired 2FA challenge", statusCode: StatusCodes.Status401Unauthorized),
            TwoFactorOutcomeType.InvalidCode => Problem(title: "Invalid authenticator code", statusCode: StatusCodes.Status401Unauthorized),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("login/otp/verify")]
    public async Task<IActionResult> VerifyLoginOtp(OtpVerifyRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyLoginOtpAsync(request.ChallengeToken, request.Code, cancellationToken);

        return result.Outcome switch
        {
            OtpVerifyOutcomeType.Success => Ok(new OtpVerifyResponse(
                result.AccessToken!.Token,
                result.AccessToken.ExpiresAtUtc,
                result.RefreshToken!,
                new UserDataResponse(
                    result.UserData!.UserId,
                    result.UserData.FullName,
                    result.UserData.Email,
                    result.UserData.PhoneNumber,
                    result.UserData.UserClass,
                    result.UserData.UserType))),
            OtpVerifyOutcomeType.InvalidChallenge => Problem(title: "Invalid or expired OTP challenge", statusCode: StatusCodes.Status401Unauthorized),
            OtpVerifyOutcomeType.InvalidCode => Problem(title: "Invalid OTP code", statusCode: StatusCodes.Status401Unauthorized),
            OtpVerifyOutcomeType.TooManyAttempts => Problem(title: "Too many incorrect attempts — request a new code", statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RequestPasswordResetAsync(request.Email, ip, cancellationToken);

        return result.Outcome switch
        {
            PasswordResetRequestOutcomeType.Accepted => Ok(new ForgotPasswordResponse(result.ChallengeToken!)),
            PasswordResetRequestOutcomeType.LockedOut => Problem(title: "Too many failed attempts — try again later", statusCode: StatusCodes.Status429TooManyRequests),
            PasswordResetRequestOutcomeType.TooSoon => Problem(title: "A code was sent recently — wait a minute before requesting another", statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request.ChallengeToken, request.Code, request.NewPassword, cancellationToken);

        // InvalidChallenge and InvalidCode share one message: a decoy challenge (unknown email) must
        // be indistinguishable from a real one with a mistyped code.
        return result.Outcome switch
        {
            PasswordResetOutcomeType.Success => NoContent(),
            PasswordResetOutcomeType.InvalidChallenge or PasswordResetOutcomeType.InvalidCode =>
                Problem(title: "Invalid or expired reset code", statusCode: StatusCodes.Status400BadRequest),
            PasswordResetOutcomeType.TooManyAttempts => Problem(title: "Too many incorrect attempts — request a new code", statusCode: StatusCodes.Status429TooManyRequests),
            PasswordResetOutcomeType.WeakPassword => Problem(title: "Password must be at least 8 characters", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);

        return result.Outcome switch
        {
            RefreshOutcomeType.Success => Ok(new TokenResponse(result.AccessToken!.Token, result.AccessToken.ExpiresAtUtc, result.RefreshToken!)),
            RefreshOutcomeType.InvalidOrExpired => Problem(title: "Invalid or expired refresh token", statusCode: StatusCodes.Status401Unauthorized),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
