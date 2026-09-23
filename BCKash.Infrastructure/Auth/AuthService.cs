using System.Security.Cryptography;
using BCKash.Application.Auth;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private const int OtpMaxAttempts = 5;

    private readonly BCKashDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginThrottleService _throttleService;
    private readonly IPermissionService _permissionService;
    private readonly IEmailSender _emailSender;
    private readonly IOtpSmsSender _otpSmsSender;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        BCKashDbContext db,
        IPasswordHasher passwordHasher,
        ITotpService totpService,
        IJwtTokenService jwtTokenService,
        ILoginThrottleService throttleService,
        IPermissionService permissionService,
        IEmailSender emailSender,
        IOtpSmsSender otpSmsSender,
        ILogger<AuthService> logger,
        IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _jwtTokenService = jwtTokenService;
        _throttleService = throttleService;
        _permissionService = permissionService;
        _emailSender = emailSender;
        _otpSmsSender = otpSmsSender;
        _logger = logger;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (await _throttleService.IsLockedOutAsync(user?.Id, ip, cancellationToken))
        {
            return new LoginResult(LoginOutcomeType.LockedOut);
        }

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            await _throttleService.RecordFailedAttemptAsync(user?.Id, ip, cancellationToken);
            return new LoginResult(LoginOutcomeType.InvalidCredentials);
        }

        if (user.Blocked)
        {
            return new LoginResult(LoginOutcomeType.Blocked);
        }

        if (user.OnboardingStatus != UserOnboardingStatus.Approved)
        {
            return new LoginResult(LoginOutcomeType.PendingOnboarding);
        }

        if (!AccessWindowEvaluator.IsWithinWindow(user, DateTime.Now))
        {
            return new LoginResult(LoginOutcomeType.OutsideAccessWindow);
        }

        if (user.EnableGoogle2fa)
        {
            return new LoginResult(LoginOutcomeType.RequiresTwoFactor,
                TwoFactorChallengeToken: _jwtTokenService.GenerateTwoFactorChallengeToken(user.Id));
        }

        var otpChallengeToken = await IssueLoginOtpAsync(user, cancellationToken);
        return new LoginResult(LoginOutcomeType.RequiresOtp, OtpChallengeToken: otpChallengeToken);
    }

    public async Task<OtpVerifyResult> VerifyLoginOtpAsync(string challengeToken, string code, CancellationToken cancellationToken = default)
    {
        var claims = _jwtTokenService.ValidateLoginOtpChallengeToken(challengeToken);
        if (claims is null)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidChallenge);
        }

        var (userId, otpId) = claims.Value;
        var otp = await _db.LoginOtps.FirstOrDefaultAsync(o => o.Id == otpId && o.UserId == userId, cancellationToken);
        if (otp is null || otp.ConsumedAtUtc is not null || otp.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidChallenge);
        }

        if (otp.Attempts >= OtpMaxAttempts)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.TooManyAttempts);
        }

        if (otp.CodeHash != Hash(code))
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidCode);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidChallenge);
        }

        otp.ConsumedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, refreshToken) = await IssueTokensAsync(user, cancellationToken);
        var userType = await GetUserTypeSlugAsync(user.Id, cancellationToken);
        var userData = new UserLoginData(
            user.Id.ToString(),
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email,
            user.Phone,
            user.UserClass?.ToString(),
            userType);

        return new OtpVerifyResult(OtpVerifyOutcomeType.Success, accessToken, refreshToken, userData);
    }

    public async Task<TwoFactorResult> VerifyTwoFactorAsync(string challengeToken, string totpCode, CancellationToken cancellationToken = default)
    {
        var userId = _jwtTokenService.ValidateTwoFactorChallengeToken(challengeToken);
        if (userId is null)
        {
            return new TwoFactorResult(TwoFactorOutcomeType.InvalidChallenge);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.Google2faSecret))
        {
            return new TwoFactorResult(TwoFactorOutcomeType.InvalidChallenge);
        }

        if (!_totpService.ValidateCode(user.Google2faSecret, totpCode))
        {
            return new TwoFactorResult(TwoFactorOutcomeType.InvalidCode);
        }

        var (accessToken, refreshToken) = await IssueTokensAsync(user, cancellationToken);
        return new TwoFactorResult(TwoFactorOutcomeType.Success, accessToken, refreshToken);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hashed = Hash(refreshToken);
        var persistence = await _db.Persistences
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Code == hashed, cancellationToken);

        if (persistence is null)
        {
            return new RefreshResult(RefreshOutcomeType.InvalidOrExpired);
        }

        var expiresAt = (persistence.CreatedAt ?? DateTime.UtcNow).AddDays(_jwtSettings.RefreshTokenDays);
        _db.Persistences.Remove(persistence);

        if (expiresAt <= DateTime.UtcNow || persistence.User.Blocked)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new RefreshResult(RefreshOutcomeType.InvalidOrExpired);
        }

        var (accessToken, newRefreshToken) = await IssueTokensAsync(persistence.User, cancellationToken);
        return new RefreshResult(RefreshOutcomeType.Success, accessToken, newRefreshToken);
    }

    private async Task<(AccessToken AccessToken, string RefreshToken)> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var permissionSlugs = await _permissionService.GetEffectivePermissionSlugsAsync(user.Id, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissionSlugs);

        var rawRefreshToken = GenerateRefreshTokenValue();
        _db.Persistences.Add(new Persistence
        {
            UserId = user.Id,
            Code = Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (accessToken, rawRefreshToken);
    }

    private async Task<string> IssueLoginOtpAsync(User user, CancellationToken cancellationToken)
    {
        var code = GenerateNumericOtp(6);
        var otp = new LoginOtp
        {
            UserId = user.Id,
            CodeHash = Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow,
        };
        _db.LoginOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        await SendLoginOtpAsync(user, code, cancellationToken);

        return _jwtTokenService.GenerateLoginOtpChallengeToken(user.Id, otp.Id);
    }

    // Delivery is best-effort per channel: a failure on one (a bad Termii key, SMTP relay
    // hiccup, etc.) must not block the other from going out, and must never surface as a
    // login failure — the OTP row already exists, so the user can still receive/retry it.
    private async Task SendLoginOtpAsync(User user, string code, CancellationToken cancellationToken)
    {
        var message = $"Your BCKash login verification code is {code}. It expires in 5 minutes.";

        try
        {
            await _emailSender.SendAsync(user.Email, "Your BCKash login code", message, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to email login OTP to {Email}", user.Email);
        }

        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            try
            {
                await _otpSmsSender.SendAsync(user.Phone!, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to SMS login OTP to {Phone}", user.Phone);
            }
        }
    }

    private async Task<string?> GetUserTypeSlugAsync(int userId, CancellationToken cancellationToken) =>
        await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

    private static string GenerateNumericOtp(int digits)
    {
        var max = (int)Math.Pow(10, digits);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', digits));
    }

    private static string GenerateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
