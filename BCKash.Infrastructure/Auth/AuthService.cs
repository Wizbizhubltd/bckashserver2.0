using System.Security.Cryptography;
using System.Text;
using BCKash.Application.Auth;
using BCKash.Application.Organization;
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

    // Long enough for an SMS that's queued or slowed by the network to still be usable on arrival.
    private static readonly TimeSpan LoginOtpLifetime = TimeSpan.FromMinutes(10);

    // How many earlier codes in a resend chain are still accepted (see LoginOtp.PreviousOtpId).
    private const int OtpChainDepth = 3;
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LoginOtpResendCooldown = TimeSpan.FromSeconds(60);

    private readonly BCKashDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginThrottleService _throttleService;
    private readonly IPermissionService _permissionService;
    private readonly IOtpDispatcher _otpDispatcher;
    private readonly ICompanyProfileProvider _companyProfile;
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
        ICompanyProfileProvider companyProfile,
        IOptions<JwtSettings> jwtSettings,
        IOptions<OtpSettings> otpSettings)
    {
        _db = db;
        _companyProfile = companyProfile;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _jwtTokenService = jwtTokenService;
        _throttleService = throttleService;
        _permissionService = permissionService;
        _otpDispatcher = otpDispatcher;
        _jwtSettings = jwtSettings.Value;
        _otpSettings = otpSettings.Value;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string? ip, string? portal = null, CancellationToken cancellationToken = default)
    {
        var user = await FindByEmailAsync(email, cancellationToken);

        var lockedUntil = await _throttleService.GetLockedUntilAsync(user?.Id, ip, cancellationToken);
        if (lockedUntil is not null)
        {
            return new LoginResult(LoginOutcomeType.LockedOut, LockedUntilUtc: lockedUntil);
        }

        if (user is null || !PasswordMatches(password, user.PasswordHash))
        {
            await _throttleService.RecordFailedAttemptAsync(user?.Id, ip, cancellationToken);
            return new LoginResult(LoginOutcomeType.InvalidCredentials);
        }

        // The right password wipes the slate: earlier typos shouldn't count towards a later lockout.
        await _throttleService.ClearFailedAttemptsAsync(user.Id, cancellationToken);

        if (user.Blocked)
        {
            return new LoginResult(LoginOutcomeType.Blocked);
        }

        if (user.OnboardingStatus != UserOnboardingStatus.Approved)
        {
            return new LoginResult(LoginOutcomeType.PendingOnboarding);
        }

        if (!PortalAccessRules.CanSignIn(await GetUserTypeSlugAsync(user.Id, cancellationToken), portal))
        {
            return new LoginResult(LoginOutcomeType.WrongPortal);
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
        var otp = await _db.LoginOtps.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == otpId && o.UserId == userId, cancellationToken);
        if (otp is null || otp.ConsumedAtUtc is not null || otp.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidChallenge);
        }

        if (otp.Attempts >= OtpMaxAttempts)
        {
            return new OtpVerifyResult(OtpVerifyOutcomeType.TooManyAttempts);
        }

        var chain = await MatchingChainAsync(otp, code, cancellationToken);
        if (chain is null)
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(OtpVerifyOutcomeType.InvalidCode);
        }

        // Consuming the codes is saved together with the new session (one SaveChanges in IssueTokensAsync).
        var user = otp.User;
        await ConsumeAsync(chain, cancellationToken);

        var userType = await GetUserTypeSlugAsync(user.Id, cancellationToken);
        var (accessToken, refreshToken) = await StartSessionAsync(user, userType, deviceId, cancellationToken);
        var userData = BuildUserData(user, userType);

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

        // The previous code stays valid until it expires (linked as PreviousOtpId), so if its SMS turns
        // up after this new one, typing either still works.
        var newChallengeToken = await IssueLoginOtpAsync(user, cancellationToken, previousOtpId: otp.Id);
        return new OtpResendResult(OtpResendOutcomeType.Resent, newChallengeToken);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await FindByEmailAsync(email, cancellationToken);

        // Only the IP-wide lock applies here, not the account lock: resetting the password is the
        // way out of a locked account. The SMS cooldown below still limits how often it can run.
        var lockedUntil = await _throttleService.GetIpLockedUntilAsync(ip, cancellationToken);
        if (lockedUntil is not null)
        {
            return new PasswordResetRequestResult(PasswordResetRequestOutcomeType.LockedOut, LockedUntilUtc: lockedUntil);
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

        // A code from an earlier request may still be on its way — keep it usable alongside this one.
        var now = DateTime.UtcNow;
        var previousOtpId = await _db.LoginOtps
            .Where(o => o.UserId == user.Id && o.ConsumedAtUtc == null && o.ExpiresAtUtc > now)
            .OrderByDescending(o => o.Id)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var code = GenerateNumericOtp(6);
        var otp = new LoginOtp
        {
            UserId = user.Id,
            CodeHash = Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(PasswordResetOtpLifetime),
            CreatedAt = DateTime.UtcNow,
            PreviousOtpId = previousOtpId,
        };
        _db.LoginOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        var companyName = (await _companyProfile.GetAsync(cancellationToken)).Name;
        await SendOtpAsync(
            user,
            $"Your {companyName} password reset code",
            $"Your {companyName} password reset code is {code}. It expires in {(int)PasswordResetOtpLifetime.TotalMinutes} minutes. If you didn't request this, ignore this message.",
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

        var chain = await MatchingChainAsync(otp, code, cancellationToken);
        if (chain is null)
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

        await ConsumeAsync(chain, cancellationToken);
        user.PasswordHash = _passwordHasher.Hash(newPassword);

        // The user chose this password themselves, so any pending temporary-password change is done.
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;

        // Whoever prompted the reset may have been using a stolen session — end it: revoke every
        // refresh token, and clear the active session so outstanding access tokens stop working too.
        var sessions = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(sessions);
        user.ActiveSessionId = null;

        await _db.SaveChangesAsync(cancellationToken);

        // A new password unlocks an account locked by wrong guesses at the old one.
        await _throttleService.ClearFailedAttemptsAsync(user.Id, cancellationToken);
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

        var userType = await GetUserTypeSlugAsync(user.Id, cancellationToken);
        var (accessToken, refreshToken) = await StartSessionAsync(user, userType, deviceId, cancellationToken);
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

        var userType = await GetUserTypeSlugAsync(persistence.User.Id, cancellationToken);
        var (accessToken, newRefreshToken) = await IssueTokensAsync(persistence.User, userType, persistence.SessionId!, persistence.DeviceId, cancellationToken);
        return new RefreshResult(RefreshOutcomeType.Success, accessToken, newRefreshToken);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new PasswordChangeResult(PasswordChangeOutcomeType.NotFound);
        }

        if (!PasswordMatches(currentPassword, user.PasswordHash))
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
        user.PasswordChangedAt = DateTime.UtcNow;

        // Stay signed in on this device, but reissue the tokens: the current ones carry the
        // password-change-required claim, and older refresh tokens predate the new password.
        var staleRefreshTokens = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(staleRefreshTokens);

        var userType = await GetUserTypeSlugAsync(user.Id, cancellationToken);
        var (accessToken, refreshToken) = user.ActiveSessionId is null
            ? await StartSessionAsync(user, userType, user.ActiveDeviceId, cancellationToken)
            : await IssueTokensAsync(user, userType, user.ActiveSessionId, user.ActiveDeviceId, cancellationToken);
        var userData = BuildUserData(user, userType);

        return new PasswordChangeResult(PasswordChangeOutcomeType.Success, accessToken, refreshToken, userData);
    }

    /// <summary>
    /// Completes a sign-in as a brand-new session, which signs the user out everywhere else: every
    /// other device's refresh tokens are revoked here, and their access tokens stop being accepted
    /// because their session id no longer matches (see <see cref="IActiveSessionChecker"/>).
    /// </summary>
    private async Task<(AccessToken AccessToken, string RefreshToken)> StartSessionAsync(User user, string? userType, string? deviceId, CancellationToken cancellationToken)
    {
        var otherSessions = await _db.Persistences.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        _db.Persistences.RemoveRange(otherSessions);

        var sessionId = Guid.NewGuid().ToString("N");
        user.ActiveSessionId = sessionId;
        user.ActiveDeviceId = deviceId;

        return await IssueTokensAsync(user, userType, sessionId, deviceId, cancellationToken);
    }

    private async Task<(AccessToken AccessToken, string RefreshToken)> IssueTokensAsync(User user, string? userType, string sessionId, string? deviceId, CancellationToken cancellationToken)
    {
        var permissionSlugs = await _permissionService.GetEffectivePermissionSlugsAsync(user.Id, cancellationToken);
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

    private async Task<string> IssueLoginOtpAsync(User user, CancellationToken cancellationToken, int? previousOtpId = null)
    {
        var code = GenerateNumericOtp(6);
        var otp = new LoginOtp
        {
            UserId = user.Id,
            CodeHash = Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(LoginOtpLifetime),
            CreatedAt = DateTime.UtcNow,
            PreviousOtpId = previousOtpId,
        };
        _db.LoginOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        var companyName = (await _companyProfile.GetAsync(cancellationToken)).Name;
        await SendOtpAsync(
            user,
            $"Your {companyName} login code",
            $"Your {companyName} login verification code is {code}. It expires in {(int)LoginOtpLifetime.TotalMinutes} minutes.",
            cancellationToken);

        return _jwtTokenService.GenerateLoginOtpChallengeToken(user.Id, otp.Id);
    }

    private static UserLoginData BuildUserData(User user, string? userType) => new(
        user.Id.ToString(),
        $"{user.FirstName} {user.LastName}".Trim(),
        user.Email,
        user.Phone,
        user.UserClass?.ToString(),
        userType,
        user.MustChangePassword);

    // No LOWER() here: that would stop MariaDB using users_email_unique and scan every user on each
    // sign-in. The email column's collation (utf8mb4_uca1400_ai_ci) already compares case-insensitively.
    private Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim();
        return _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    private Task SendOtpAsync(User user, string subject, string message, CancellationToken cancellationToken) =>
        _otpDispatcher.DispatchAsync(new OtpMessage(user.Email, user.Phone, subject, message), cancellationToken);

    private async Task<string?> GetUserTypeSlugAsync(int userId, CancellationToken cancellationToken) =>
        await _db.RoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.Role.Slug)
            .Where(slug => UserTypeSlugs.All.Contains(slug))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Matches the code sent for <paramref name="otp"/>, or the configured master OTP (see <see cref="OtpSettings.MasterOtp"/>).</summary>
    /// <summary>
    /// The challenge's code, or — if the typed code is an earlier one from the same resend chain that
    /// hasn't expired or been used — the chain up to it. Null when nothing matches.
    /// </summary>
    private async Task<List<LoginOtp>?> MatchingChainAsync(LoginOtp otp, string code, CancellationToken cancellationToken)
    {
        var chain = new List<LoginOtp> { otp };
        if (IsValidOtpCode(otp, code))
        {
            return chain;
        }

        var hash = Hash(DigitsOnly(code));
        var now = DateTime.UtcNow;
        var previousId = otp.PreviousOtpId;
        for (var depth = 0; depth < OtpChainDepth && previousId is not null; depth++)
        {
            var previous = await _db.LoginOtps.FirstOrDefaultAsync(o => o.Id == previousId && o.UserId == otp.UserId, cancellationToken);
            if (previous is null)
            {
                break;
            }

            chain.Add(previous);
            if (previous.ConsumedAtUtc is null && previous.ExpiresAtUtc > now && previous.CodeHash == hash)
            {
                return chain;
            }

            previousId = previous.PreviousOtpId;
        }

        return null;
    }

    /// <summary>
    /// Spends the matched codes and every other outstanding code for the same account — once someone
    /// has signed in or reset their password, no code sent earlier (resends, repeated requests) may be
    /// used again. Saved by the caller's SaveChanges.
    /// </summary>
    private async Task ConsumeAsync(List<LoginOtp> chain, CancellationToken cancellationToken)
    {
        var userId = chain[0].UserId;
        var now = DateTime.UtcNow;
        var outstanding = await _db.LoginOtps.Where(o => o.UserId == userId && o.ConsumedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var code in chain.Concat(outstanding).Where(c => c.ConsumedAtUtc is null))
        {
            code.ConsumedAtUtc = now;
        }
    }

    /// <summary>Codes pasted from SMS or email often carry spaces ("123 456", "123456 ") — only the digits count.</summary>
    private static string DigitsOnly(string code) => new(code.Where(char.IsDigit).ToArray());

    /// <summary>
    /// The typed password, or the same with surrounding spaces removed: staff copy temporary passwords
    /// out of emails and SMS on their phones, and the copy often picks up a trailing space.
    /// </summary>
    private bool PasswordMatches(string password, string hash)
    {
        if (_passwordHasher.Verify(password, hash))
        {
            return true;
        }

        var trimmed = password.Trim();
        return trimmed.Length > 0 && trimmed.Length != password.Length && _passwordHasher.Verify(trimmed, hash);
    }

    private bool IsValidOtpCode(LoginOtp otp, string code)
    {
        if (otp.CodeHash == Hash(DigitsOnly(code)))
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
