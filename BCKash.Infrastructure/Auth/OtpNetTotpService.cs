using BCKash.Application.Auth;
using OtpNet;

namespace BCKash.Infrastructure.Auth;

public class OtpNetTotpService : ITotpService
{
    public string GenerateBase32Secret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public bool ValidateCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        // One-step tolerance either side of "now" to absorb normal client/server clock drift.
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
