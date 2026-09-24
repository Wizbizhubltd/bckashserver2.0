using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCKash.Application.Auth;
using BCKash.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BCKash.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private const string TwoFactorChallengeType = "2fa_challenge";
    private const string LoginOtpChallengeType = "otp_challenge";
    private const string PasswordResetChallengeType = "password_reset_challenge";

    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public AccessToken GenerateAccessToken(User user, IReadOnlyCollection<string> permissionSlugs, string? userType, string sessionId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(AuthClaimTypes.SessionId, sessionId),
        };

        if (user.OfficeId.HasValue)
        {
            claims.Add(new Claim(AuthClaimTypes.OfficeId, user.OfficeId.Value.ToString()));
        }

        if (userType is not null)
        {
            claims.Add(new Claim(AuthClaimTypes.UserType, userType));
        }

        if (user.MustChangePassword)
        {
            claims.Add(new Claim(AuthClaimTypes.PasswordChangeRequired, "true"));
        }

        claims.AddRange(permissionSlugs.Select(slug => new Claim(AuthClaimTypes.Permission, slug)));

        var token = CreateToken(claims, expiresAt);
        return new AccessToken(token, expiresAt);
    }

    public string GenerateTwoFactorChallengeToken(int userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("type", TwoFactorChallengeType),
        };

        // Five minutes is enough to type a 6-digit code, short enough to limit replay if intercepted.
        return CreateToken(claims, DateTime.UtcNow.AddMinutes(5));
    }

    public int? ValidateTwoFactorChallengeToken(string challengeToken)
    {
        var principal = ValidateToken(challengeToken);
        if (principal is null)
        {
            return null;
        }

        var type = principal.FindFirst("type")?.Value;
        // JwtSecurityTokenHandler remaps "sub" to ClaimTypes.NameIdentifier on validation by
        // default (MapInboundClaims) — look it up under the remapped type, not the original "sub".
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (type != TwoFactorChallengeType || !int.TryParse(sub, out var userId))
        {
            return null;
        }

        return userId;
    }

    // Five minutes is enough to receive and type a 6-digit code, short enough to limit replay if intercepted.
    public string GenerateLoginOtpChallengeToken(int userId, int otpId) =>
        CreateOtpChallengeToken(userId, otpId, LoginOtpChallengeType, TimeSpan.FromMinutes(5));

    public (int UserId, int OtpId)? ValidateLoginOtpChallengeToken(string challengeToken) =>
        ValidateOtpChallengeToken(challengeToken, LoginOtpChallengeType);

    // Longer than the login window since the user also has to choose and confirm a new password.
    public string GeneratePasswordResetChallengeToken(int userId, int otpId) =>
        CreateOtpChallengeToken(userId, otpId, PasswordResetChallengeType, TimeSpan.FromMinutes(15));

    public (int UserId, int OtpId)? ValidatePasswordResetChallengeToken(string challengeToken) =>
        ValidateOtpChallengeToken(challengeToken, PasswordResetChallengeType);

    private string CreateOtpChallengeToken(int userId, int otpId, string type, TimeSpan lifetime)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("type", type),
            new Claim("otp_id", otpId.ToString()),
        };

        return CreateToken(claims, DateTime.UtcNow.Add(lifetime));
    }

    private (int UserId, int OtpId)? ValidateOtpChallengeToken(string challengeToken, string expectedType)
    {
        var principal = ValidateToken(challengeToken);
        if (principal is null)
        {
            return null;
        }

        var type = principal.FindFirst("type")?.Value;
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var otpIdClaim = principal.FindFirst("otp_id")?.Value;

        if (type != expectedType || !int.TryParse(sub, out var userId) || !int.TryParse(otpIdClaim, out var otpId))
        {
            return null;
        }

        return (userId, otpId);
    }

    private string CreateToken(IEnumerable<Claim> claims, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
