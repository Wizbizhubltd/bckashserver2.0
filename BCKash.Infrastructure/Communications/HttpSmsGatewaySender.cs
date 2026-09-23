using System.Web;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;

namespace BCKash.Infrastructure.Communications;

/// <summary>
/// Generic URL/template-based SMS sending (FR-COM-3) — a GET request against the gateway's
/// configured <see cref="SmsGateway.Url"/>, with the recipient phone, message text, and a
/// sender id appended as query parameters, under whatever parameter NAMES that gateway expects
/// (<see cref="SmsGateway.ToName"/>/<see cref="SmsGateway.MsgName"/>/<see cref="SmsGateway.FromName"/>
/// respectively — the legacy schema stores these as configurable names, not fixed "to"/"message"/
/// "from" keys, so different providers' differing query-string conventions can be matched
/// without a code change). <see cref="SmsGateway.Name"/> is used as the sender-id VALUE sent
/// under the <see cref="SmsGateway.FromName"/> parameter — the legacy schema has no separate
/// "sender id value" column, so this is a documented modeling assumption. Any static query
/// parameters already present in <see cref="SmsGateway.Url"/> (e.g. an API key) are preserved.
/// </summary>
public class HttpSmsGatewaySender : ISmsSender
{
    private readonly HttpClient _httpClient;

    public HttpSmsGatewaySender(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendAsync(SmsGateway gateway, string toPhone, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gateway.Url))
        {
            return;
        }

        var builder = new UriBuilder(gateway.Url);
        var query = HttpUtility.ParseQueryString(builder.Query);

        if (!string.IsNullOrWhiteSpace(gateway.ToName))
        {
            query[gateway.ToName] = toPhone;
        }

        if (!string.IsNullOrWhiteSpace(gateway.MsgName))
        {
            query[gateway.MsgName] = message;
        }

        if (!string.IsNullOrWhiteSpace(gateway.FromName))
        {
            query[gateway.FromName] = gateway.Name;
        }

        builder.Query = query.ToString();

        var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
