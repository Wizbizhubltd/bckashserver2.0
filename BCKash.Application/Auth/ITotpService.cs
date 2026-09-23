namespace BCKash.Application.Auth;

/// <summary>
/// TOTP against `users.google2fa_secret` (FR-SEC-3). Legacy Google2FA-format base32
/// secrets are standard TOTP-SHA1/6-digit/30s-step, same defaults Otp.NET uses.
/// </summary>
public interface ITotpService
{
    string GenerateBase32Secret();
    bool ValidateCode(string base32Secret, string code);
}
