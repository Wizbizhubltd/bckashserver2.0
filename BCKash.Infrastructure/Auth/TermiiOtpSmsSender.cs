using System.Net.Http.Json;
using BCKash.Application.Auth;
using BCKash.Infrastructure.Communications;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Auth;

/// <summary>
/// Sends login OTP codes via Termii's SMS API (BCKash's chosen provider for this — see
/// <see cref="SmsSettings"/>). A POST against `Sms:Provider` with the shape Termii's
/// `/api/sms/send` endpoint expects.
/// </summary>
public class TermiiOtpSmsSender : IOtpSmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsSettings _settings;

    public TermiiOtpSmsSender(HttpClient httpClient, IOptions<SmsSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Provider) || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return;
        }

        var payload = new
        {
            to = NormalizeToNigerianInternationalFormat(toPhone),
            from = _settings.SenderId,
            sms = message,
            type = "plain",
            channel = "generic",
            api_key = _settings.ApiKey,
        };

        var response = await _httpClient.PostAsJsonAsync(_settings.Provider, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Termii rejects locally-formatted numbers ("Recipient phone number is not a valid,
    /// dialable number") — it wants the international dial format with no leading zero. The
    /// legacy `users.phone` column stores whatever a staff member typed, typically Nigerian
    /// local format (0801...), so that's normalized here rather than at every call site.
    /// </summary>
    private static string NormalizeToNigerianInternationalFormat(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 11 && digits.StartsWith('0') ? "234" + digits[1..] : digits;
    }
}
