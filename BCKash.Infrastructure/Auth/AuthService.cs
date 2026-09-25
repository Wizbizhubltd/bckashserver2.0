using System.Security.Cryptography;
using System.Text;
using BCKash.Application.Auth;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private const int OtpMaxAttempts = 5;
    private const int PasswordMinLength = 8;
    private static readonly TimeSpan PasswordResetOtpLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LoginOtpResendCooldown = TimeSpan.FromSeconds(60);

    private readonly BCKashDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginThrottleService _throttleService;
    private readonly IPermissionService _permissionService;
    private readonly IOtpDispatcher _otpDispatcher;
    private readonly JwtSettings _jwtSettings;
    private readonly OtpSettings _otpSettings;

    public AuthService(
        BCKashDbContext db,
        IPasswordHasher passwordHasher,
        ITotpService totpService,
        IJwtTokenService jwtTokenService,
        ILoginThrottleService throttleService,
        IPermissionService permissionService,
        IOtpDispatcher otpDispatcher,
        IOptions<JwtSettings> jwtSettings,
        IOptions<OtpSettings> otpSettings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _jwtTokenService = jwtTokenService;
        _throttleService = throttleService;
        _permissionService = permissionService;
        _otpDispatcher = otpDispatcher;
        _jwtSettings = jwtSettings.Value;
        _otpSettings = otpSettings.Value;
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

    public async Task<OtpVerifyResult> VerifyLoginOtpAsync(string challengeToken, string code, string? deviceId, CancellationToken cancellationToken = default)
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

        if (!IsValidOtpCode(otp, code))
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

        var (accessToken, refreshToken) = await StartSessionAsync(user, deviceId, cancellationToken);
        var userData = await BuildUserDataAsync(user, cancellationToken);

        return new OtpVerifyResult(OtpVerifyOutcomeType.Success, accessToken, refreshToken, userData);
    }

    public async Task<OtpResendResult> ResendLoginOtpAsync(string challengeToken, CancellationToken cancellationToken = default)
    {
        var claims = _jwtTokenService.ValidateLoginOtpChallengeToken(challengeToken);
        if (claims is null)
        {
            return new OtpResendResult(OtpResendOutcomeType.InvalidChallenge);
        }

        // An OTP locked by too many wrong attempts can still be replaced — that's exactly what the
        // TooManyAttempts error tells the user to do — but a consumed one means the login finished
        // (or the code was already superseded by an earlier resend).
        var (userId, otpId) = claims.Value;
        var otp = await _db.LoginOtps.FirstOrDefaultAsync(o => o.Id == otpId && o.UserId == userId, cancellationToken);
        if (otp is null || otp.ConsumedAtUtc is not null)
        {
            return new OtpResendResult(OtpResendOutcomeType.InvalidChallenge);
        }

        // Re-check what LoginAsync checked, in case the account changed after the password step.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.Blocked || user.OnboardingStatus != UserOnboardingStatus.Approved)
        {
            return new OtpResendResult(OtpResendOutcomeType.InvalidChallenge);
        }

        // Every resend sends an SMS, so cap how often one account can trigger it.
        var cooldownStart = DateTime.UtcNow - LoginOtpResendCooldown;
        if (await _db.LoginOtps.AnyAsync(o => o.UserId == user.Id && o.CreatedAt > cooldownStart, cancellationToken))
        {
            return new OtpResendResult(OtpResendOutcomeType.TooSoon);
        }

        // Retire the old code so only the newest one sent can complete the login.
        otp.ConsumedAtUtc = DateTime.UtcNow;
        var newChallengeToken = await IssueLoginOtpAsync(user, cancellationToken);
        return new OtpResendResult(OtpResendOutcomeType.Resent, newChallengeToken);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower(), cancellationToken);

        if (await _throttleService.IsLockedOutAsync(user?.Id, ip, cancellationToken))
        {
            return new PasswordResetRequestResult(PasswordResetRequestOutcomeType.LockedOut);
        }

        // Blocked or not-yet-approved accounts couldn't sign in with a new password anyway, so they
        // get the same decoy challenge as an unknown email rather than a revealing error.
        if (user is null || user.Blocked || user.OnboardingStatus != UserOnboardingStatus.Approved)
        {
            return new PasswordResetRequestResult(PasswordResetRequestOutcomeType.Accepted,
                _jwtTokenService.GeneratePasswordResetChallengeToken(0, 0));
        }

        // The endpoint is anonymous and every request sends an SMS, so cap how often one account can trigger it.
        var cooldownStart = DateTime.UtcNow - PasswordResetCooldown;
        if (await _db.LoginOtps.AnyAsync(o => o.UserId == user.Id && o.CreatedAt > cooldownStart, cancellationToken))
        {
            return new PasswordResetRequestResult(PasswordResetRequestOutcomeType.TooSoon);
        }

        var code = GenerateNumericOtp(6);
        var otp = new LoginOtp
        {
            UserId = user.Id,
            CodeHash = Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(PasswordResetOtpLifetime),
            CreatedAt = DateTime.UtcNow,
        };
        _db.LoginOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        await SendOtpAsync(
            user,
            "Your BCKash password reset code",
            $"Your BCKash password reset code is {code}. It expires in {(int)PasswordResetOtpLifetime.TotalMinutes} minutes. If you didn't request this, ignore this message.",
            cancellationToken);

        return new PasswordResetRequestResult(PasswordResetRequestOutcomeType.Accepted,
            _jwtTokenService.GeneratePasswordResetChallengeToken(user.Id, otp.Id));
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string challengeToken, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var claims = _jwtTokenService.ValidatePasswordResetChallengeToken(challengeToken);
        if (claims is null)
        {
            return new PasswordResetResult(PasswordResetOutcomeType.InvalidChallenge);
        }

        var (userId, otpId) = claims.Value;
        var otp = await _db.LoginOtps.FirstOrDefaultAsync(o => o.Id == otpId && o.UserId == userId, cancellationToken);
        if (otp is null || otp.ConsumedAtUtc is not null || otp.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new PasswordResetResult(PasswordResetOutcomeType.InvalidChallenge);
        }

        if (otp.Attempts >= OtpMaxAttempts)
        {
            return new PasswordResetResult(PasswordResetOutcomeType.TooManyAttempts);
        }

        if (!IsValidOtpCode(otp, code))
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(cancellationToken);
            return new PasswordResetResult(PasswordResetOutcomeType.InvalidCode);
        }

        // Checked only after the code matches, so a weak-password rejection doesn't burn an attempt
        // and the user can simply pick a stronger one with the same code.
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < PasswordMinLength)
        {
            return new PasswordResetResult(PasswordResetOutcomeType.WeakPassword);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new PasswordResetResult(PasswordResetOutcomeType.InvalidChallenge);
        }

        otp.ConsumedAtUtc = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(newPassword);

        // The user chose this password themselves, so any pending temporary-password change is done.
        user.MustChangePassword = false;

        // Whoever prompted the reset may have been using a stolen session — end it: revoke every
        // refresh token, and clear the active session so outstanding access tokens stop working too.
        var sessions = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(sessions);
        user.ActiveSessionId = null;

        await _db.SaveChangesAsync(cancellationToken);
        return new PasswordResetResult(PasswordResetOutcomeType.Success);
    }

    public async Task<TwoFactorResult> VerifyTwoFactorAsync(string challengeToken, string totpCode, string? deviceId, CancellationToken cancellationToken = default)
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

        var (accessToken, refreshToken) = await StartSessionAsync(user, deviceId, cancellationToken);
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

        // A refresh token only lives as long as its session: once the user signs in elsewhere (or
        // was signed in before sessions were tracked), it can't be used to keep the old device going.
        var sessionEnded = persistence.SessionId is null || persistence.SessionId != persistence.User.ActiveSessionId;
        if (expiresAt <= DateTime.UtcNow || persistence.User.Blocked || sessionEnded)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new RefreshResult(RefreshOutcomeType.InvalidOrExpired);
        }

        var (accessToken, newRefreshToken) = await IssueTokensAsync(persistence.User, persistence.SessionId!, persistence.DeviceId, cancellationToken);
        return new RefreshResult(RefreshOutcomeType.Success, accessToken, newRefreshToken);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new PasswordChangeResult(PasswordChangeOutcomeType.NotFound);
        }

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return new PasswordChangeResult(PasswordChangeOutcomeType.InvalidCurrentPassword);
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < PasswordMinLength)
        {
            return new PasswordChangeResult(PasswordChangeOutcomeType.WeakPassword);
        }

        if (newPassword == currentPassword)
        {
            return new PasswordChangeResult(PasswordChangeOutcomeType.SameAsCurrent);
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.MustChangePassword = false;

        // Stay signed in on this device, but reissue the tokens: the current ones carry the
        // password-change-required claim, and older refresh tokens predate the new password.
        var staleRefreshTokens = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(staleRefreshTokens);

        var (accessToken, refreshToken) = user.ActiveSessionId is null
            ? await StartSessionAsync(user, user.ActiveDeviceId, cancellationToken)
            : await IssueTokensAsync(user, user.ActiveSessionId, user.ActiveDeviceId, cancellationToken);
        var userData = await BuildUserDataAsync(user, cancellationToken);

        return new PasswordChangeResult(PasswordChangeOutcomeType.Success, accessToken, refreshToken, userData);
    }

    /// <summary>
    /// Completes a sign-in as a brand-new session, which signs the user out everywhere else: every
    /// other device's refresh tokens are revoked here, and their access tokens stop being accepted
    /// because their session id no longer matches (see <see cref="IActiveSessionChecker"/>).
    /// </summary>
    private async Task<(AccessToken AccessToken, string RefreshToken)> StartSessionAsync(User user, string? deviceId, CancellationToken cancellationToken)
    {
        var otherSessions = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(otherSessions);

        var sessionId = Guid.NewGuid().ToString("N");
        user.ActiveSessionId = sessionId;
        user.ActiveDeviceId = deviceId;

        return await IssueTokensAsync(user, sessionId, deviceId, cancellationToken);
    }

    private async Task<(AccessToken AccessToken, string RefreshToken)> IssueTokensAsync(User user, string sessionId, string? deviceId, CancellationToken cancellationToken)
    {
        var permissionSlugs = await _permissionService.GetEffectivePermissionSlugsAsync(user.Id, cancellationToken);
        var userType = await GetUserTypeSlugAsync(user.Id, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissionSlugs, userType, sessionId);

        var rawRefreshToken = GenerateRefreshTokenValue();
        _db.Persistences.Add(new Persistence
        {
            UserId = user.Id,
            Code = Hash(rawRefreshToken),
            SessionId = sessionId,
            DeviceId = deviceId,
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

        await SendOtpAsync(
            user,
            "Your BCKash login code",
            $"Your BCKash login verification code is {code}. It expires in 5 minutes.",
            cancellationToken);

        return _jwtTokenService.GenerateLoginOtpChallengeToken(user.Id, otp.Id);
    }

    private async Task<UserLoginData> BuildUserDataAsync(User user, CancellationToken cancellationToken) => new(
        user.Id.ToString(),
        $"{user.FirstName} {user.LastName}".Trim(),
        user.Email,
        user.Phone,
        user.UserClass?.ToString(),
        await GetUserTypeSlugAsync(user.Id, cancellationToken),
        user.MustChangePassword);

    private Task SendOtpAsync(User user, string subject, string message, CancellationToken cancellationToken) =>
        _otpDispatcher.DispatchAsync(new OtpMessage(user.Email, user.Phone, subject, message), cancellationToken);

    private async Task<string?> GetUserTypeSlugAsync(int userId, CancellationToken cancellationToken) =>
        await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Matches the code sent for <paramref name="otp"/>, or the configured master OTP (see <see cref="OtpSettings.MasterOtp"/>).</summary>
    private bool IsValidOtpCode(LoginOtp otp, string code)
    {
        if (otp.CodeHash == Hash(code))
        {
            return true;
        }

        var masterOtp = _otpSettings.MasterOtp?.Trim();
        return !string.IsNullOrEmpty(masterOtp)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code.Trim()), Encoding.UTF8.GetBytes(masterOtp));
    }

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
