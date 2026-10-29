using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BCKash.Application.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Clients;

/// <summary>
/// BVN lookups through the BC Kash MFB core gateway ("BC Kash MFB API Integration Documentation"):
/// POST {BaseUrl}/initialisation/init with Email + Password returns Authorisation.auth and
/// Authorisation.accesscode; POST {BaseUrl}/identity/get_bvn with {"bvn"} and the headers
/// X-Auth-Signature: {auth} and Authorization: Bearer {accesscode} returns the BVN's details.
/// A BVN only counts as verified when the response says isBvnValid: true, and only isBvnValid: false
/// on an answered request means the BVN is invalid. When the gateway refuses a request (always HTTP 400,
/// with RequestStatus: false), staff see its ResponseMessage word for word — e.g. "Valid BVN is required
/// (11 digits)" — and a refused login shows the login's message the same way. Only when the gateway gives
/// no message (no answer, a timeout, an empty body) do they see <see cref="ProviderError"/>.
/// </summary>
public class BcKashGatewayBvnVerificationProvider : IBvnVerificationProvider
{
    /// <summary>What staff see when the BVN gateway fails without saying why.</summary>
    public const string ProviderError = "Network error from the BVN API provider. Contact your admin to fix this and retry later.";

    // The docs give no lifetime for auth/accesscode, and an expired session isn't refused with a 401 —
    // the gateway answers 400 / "System Error!" instead. So a session is only reused for a short while,
    // and any failed lookup on a reused session logs in again and retries once.
    // Keyed by gateway + account so a change of either never reuses the other's login.
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim LoginLock = new(1, 1);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GatewayCredentials> Credentials = new();

    private readonly HttpClient _http;
    private readonly BvnGatewaySettings _settings;
    private readonly ILogger<BcKashGatewayBvnVerificationProvider> _logger;

    public BcKashGatewayBvnVerificationProvider(HttpClient http, IOptions<BvnGatewaySettings> settings, ILogger<BcKashGatewayBvnVerificationProvider> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<BvnLookupResult> LookupAsync(string bvn, string firstName, string? middleName, string lastName, string? phone, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Email) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning("BVN gateway isn't configured — set BvnGateway__Email and BvnGateway__Password.");
            return new BvnLookupResult(BvnLookupOutcome.Unavailable, Error: ProviderError);
        }

        try
        {
            var (credentials, fresh) = await GetCredentialsAsync(forceLogin: false, cancellationToken);
            var (status, result) = await QueryAsync(bvn, credentials, cancellationToken);

            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || (!fresh && !Succeeded(status, result)))
            {
                (credentials, _) = await GetCredentialsAsync(forceLogin: true, cancellationToken);
                (status, result) = await QueryAsync(bvn, credentials, cancellationToken);
            }

            if (!Succeeded(status, result))
            {
                // Status and gateway message only — BVN data and credentials are never logged (integration note 6).
                _logger.LogWarning("BVN gateway lookup failed with HTTP {Status}: {Message}", (int)status, result?.ResponseMessage);
                return new BvnLookupResult(BvnLookupOutcome.Unavailable, Error: ProviderMessageOr(result?.ResponseMessage));
            }

            if (!result!.IsBvnValid || result.BvnDetails is null)
            {
                return new BvnLookupResult(BvnLookupOutcome.NotFound);
            }

            var details = result.BvnDetails;
            return new BvnLookupResult(
                BvnLookupOutcome.Found,
                FirstName: details.FirstName,
                MiddleName: string.IsNullOrWhiteSpace(details.OtherNames) ? null : details.OtherNames,
                LastName: details.LastName,
                Phone: details.PhoneNumber,
                BirthDate: details.Dob);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or GatewayLoginException)
        {
            _logger.LogWarning("BVN gateway lookup could not be completed: {Error}", ex.Message);
            return new BvnLookupResult(BvnLookupOutcome.Unavailable, Error: ProviderMessageOr((ex as GatewayLoginException)?.ProviderMessage));
        }
    }

    private async Task<(HttpStatusCode Status, GetBvnResponse? Result)> QueryAsync(string bvn, GatewayCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/identity/get_bvn")
        {
            Content = JsonContent.Create(new { bvn }),
        };
        request.Headers.Add("X-Auth-Signature", credentials.Auth);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessCode);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<GetBvnResponse>(cancellationToken));
        }

        // Error bodies are read too when they parse, for the gateway's ResponseMessage.
        try
        {
            return (response.StatusCode, await response.Content.ReadFromJsonAsync<GetBvnResponse>(cancellationToken));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return (response.StatusCode, null);
        }
    }

    private static bool Succeeded(HttpStatusCode status, GetBvnResponse? result) =>
        status == HttpStatusCode.OK && result is { RequestStatus: true };

    /// <summary>The session to query with, and whether it was just logged in (so a failure isn't the session's fault).</summary>
    private async Task<(GatewayCredentials Credentials, bool Fresh)> GetCredentialsAsync(bool forceLogin, CancellationToken cancellationToken)
    {
        var key = $"{_settings.BaseUrl}|{_settings.Email}";
        Credentials.TryGetValue(key, out var known);
        if (known is not null && !forceLogin && DateTime.UtcNow - known.IssuedAt < SessionLifetime)
        {
            return (known, false);
        }

        await LoginLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have logged in while this one waited.
            if (Credentials.TryGetValue(key, out var current) && !ReferenceEquals(current, known))
            {
                return (current, true);
            }

            using var response = await _http.PostAsJsonAsync(
                $"{_settings.BaseUrl.TrimEnd('/')}/initialisation/init",
                new LoginRequest(_settings.Email!, _settings.Password!),
                cancellationToken);
            var login = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken) : null;
            if (login is null || login.Error || string.IsNullOrWhiteSpace(login.Authorisation?.Auth) || string.IsNullOrWhiteSpace(login.Authorisation.AccessCode))
            {
                throw new GatewayLoginException($"login was refused (HTTP {(int)response.StatusCode}{(login?.Message is null ? string.Empty : $": {login.Message}")})", login?.Message);
            }

            var fresh = new GatewayCredentials(login.Authorisation.Auth, login.Authorisation.AccessCode, DateTime.UtcNow);
            Credentials[key] = fresh;
            return (fresh, true);
        }
        finally
        {
            LoginLock.Release();
        }
    }

    /// <summary>The gateway's own message, as staff should see it, or the fallback when it didn't send one.</summary>
    private static string ProviderMessageOr(string? message) => string.IsNullOrWhiteSpace(message) ? ProviderError : message.Trim();

    private sealed class GatewayLoginException(string message, string? providerMessage) : Exception(message)
    {
        public string? ProviderMessage { get; } = providerMessage;
    }

    private sealed record GatewayCredentials(string Auth, string AccessCode, DateTime IssuedAt);

    private sealed record LoginRequest(
        [property: JsonPropertyName("Email")] string Email,
        [property: JsonPropertyName("Password")] string Password);

    private sealed record LoginResponse(
        [property: JsonPropertyName("error")] bool Error,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("Authorisation")] LoginAuthorisation? Authorisation);

    private sealed record LoginAuthorisation(
        [property: JsonPropertyName("auth")] string? Auth,
        [property: JsonPropertyName("accesscode")] string? AccessCode);

    private sealed record GetBvnResponse(
        [property: JsonPropertyName("RequestStatus")] bool RequestStatus,
        [property: JsonPropertyName("ResponseMessage")] string? ResponseMessage,
        [property: JsonPropertyName("isBvnValid")] bool IsBvnValid,
        [property: JsonPropertyName("bvnDetails")] BvnDetails? BvnDetails);

    private sealed record BvnDetails(
        [property: JsonPropertyName("BVN")] string? Bvn,
        [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,
        [property: JsonPropertyName("FirstName")] string? FirstName,
        [property: JsonPropertyName("LastName")] string? LastName,
        [property: JsonPropertyName("OtherNames")] string? OtherNames,
        [property: JsonPropertyName("DOB")] string? Dob);
}
