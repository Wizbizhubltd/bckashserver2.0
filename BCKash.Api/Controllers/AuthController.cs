using System.Security.Claims;
using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
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
        var result = await _authService.VerifyTwoFactorAsync(request.ChallengeToken, request.Code, request.DeviceId, cancellationToken);

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
        var result = await _authService.VerifyLoginOtpAsync(request.ChallengeToken, request.Code, request.DeviceId, cancellationToken);

        return result.Outcome switch
        {
            OtpVerifyOutcomeType.Success => Ok(new OtpVerifyResponse(
                result.AccessToken!.Token,
                result.AccessToken.ExpiresAtUtc,
                result.RefreshToken!,
                ToResponse(result.UserData!))),
            OtpVerifyOutcomeType.InvalidChallenge => Problem(title: "Invalid or expired OTP challenge", statusCode: StatusCodes.Status401Unauthorized),
            OtpVerifyOutcomeType.InvalidCode => Problem(title: "Invalid OTP code", statusCode: StatusCodes.Status401Unauthorized),
            OtpVerifyOutcomeType.TooManyAttempts => Problem(title: "Too many incorrect attempts — request a new code", statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Sends a new login code for a pending OTP challenge and invalidates the previous code.
    /// Returns a new challenge token — the client must use it for the verify call from then on.
    /// </summary>
    [HttpPost("login/otp/resend")]
    public async Task<IActionResult> ResendLoginOtp(OtpResendRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResendLoginOtpAsync(request.ChallengeToken, cancellationToken);

        return result.Outcome switch
        {
            OtpResendOutcomeType.Resent => Ok(new OtpChallengeResponse(result.ChallengeToken!)),
            OtpResendOutcomeType.InvalidChallenge => Problem(title: "Your sign-in session has expired — sign in again", statusCode: StatusCodes.Status401Unauthorized),
            OtpResendOutcomeType.TooSoon => Problem(title: "A code was sent recently — wait a minute before requesting another", statusCode: StatusCodes.Status429TooManyRequests),
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

    /// <summary>
    /// Changes the signed-in user's own password. This is the one call allowed while a temporary
    /// password still has to be replaced. Returns new tokens for the same session.
    /// </summary>
    [HttpPost("password/change")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);

        return result.Outcome switch
        {
            PasswordChangeOutcomeType.Success => Ok(new ChangePasswordResponse(
                result.AccessToken!.Token, result.AccessToken.ExpiresAtUtc, result.RefreshToken!, ToResponse(result.UserData!))),
            PasswordChangeOutcomeType.NotFound => Unauthorized(),
            PasswordChangeOutcomeType.InvalidCurrentPassword => Problem(title: "Current password is incorrect", statusCode: StatusCodes.Status400BadRequest),
            PasswordChangeOutcomeType.WeakPassword => Problem(title: "Password must be at least 8 characters", statusCode: StatusCodes.Status400BadRequest),
            PasswordChangeOutcomeType.SameAsCurrent => Problem(title: "The new password must be different from the current one", statusCode: StatusCodes.Status400BadRequest),
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

    private static UserDataResponse ToResponse(UserLoginData data) =>
        new(data.UserId, data.FullName, data.Email, data.PhoneNumber, data.UserClass, data.UserType, data.MustChangePassword);
}
