using System.Net;
using System.Text;
using BCKash.Application.Clients;
using BCKash.Infrastructure.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// The BVN gateway provider against stubbed responses shaped exactly like the "BC Kash MFB API
/// Integration Documentation" samples — no calls to the real gateway.
/// </summary>
public class BcKashGatewayBvnVerificationProviderTests
{
    private const string LoginJson = """
        {"error": false, "loginCount": 1, "message": "Successfully!",
         "Authorisation": {"role": "0", "uid": "90", "auth": "sig-1", "accesscode": "token-1", "Businessname": "STARTX PAYMENTS LTD"}}
        """;

    private const string ValidBvnJson = """
        {"RequestStatus": true, "ResponseMessage": "Successful.", "isBvnValid": true,
         "bvnDetails": {"BVN": "21111111111", "phoneNumber": "08161749362", "FirstName": "MUIDEEN", "LastName": "OLADIPUPO", "OtherNames": "OLAIDE", "DOB": "29-Oct-74"}}
        """;

    [Fact]
    public async Task Logs_in_then_queries_the_bvn_with_the_documented_headers_and_maps_the_details()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/v1/initialisation/init" => Json(LoginJson),
            "/v1/identity/get_bvn" => Json(ValidBvnJson),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var result = await Provider(handler).LookupAsync("21111111111", "Muideen", null, "Oladipupo", null);

        Assert.Equal(BvnLookupOutcome.Found, result.Outcome);
        Assert.Equal(("MUIDEEN", "OLAIDE", "OLADIPUPO", "08161749362", "29-Oct-74"), (result.FirstName, result.MiddleName, result.LastName, result.Phone, result.BirthDate));

        var login = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Contains("\"Email\":", login.Body);
        Assert.Contains("\"Password\":", login.Body);

        var query = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, query.Method);
        Assert.Equal("sig-1", query.Headers["X-Auth-Signature"]);
        Assert.Equal("Bearer token-1", query.Headers["Authorization"]);
        Assert.Equal("{\"bvn\":\"21111111111\"}", query.Body);
    }

    [Fact]
    public async Task A_bvn_is_only_found_when_isBvnValid_is_true()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : Json("""{"RequestStatus": true, "ResponseMessage": "Successful.", "isBvnValid": false}"""));

        var result = await Provider(handler).LookupAsync("22222222222", "Ada", null, "Obi", null);

        Assert.Equal(BvnLookupOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Refused_credentials_are_replaced_by_logging_in_again_once()
    {
        var queries = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/initialisation/init")
            {
                return Json(LoginJson);
            }

            return ++queries == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(ValidBvnJson);
        });

        var result = await Provider(handler).LookupAsync("21111111111", "Muideen", null, "Oladipupo", null);

        Assert.Equal(BvnLookupOutcome.Found, result.Outcome);
        Assert.Equal(2, handler.Requests.Count(r => r.Path == "/v1/initialisation/init"));
    }

    [Fact]
    public async Task A_refused_login_or_failed_request_is_reported_as_unavailable()
    {
        var refusedLogin = new StubHandler(_ => Json("""{"error": true, "message": "Invalid login details"}"""));
        var refused = await Provider(refusedLogin).LookupAsync("21111111111", "Ada", null, "Obi", null);
        Assert.Equal(BvnLookupOutcome.Unavailable, refused.Outcome);
        Assert.Equal("Invalid login details", refused.Error);

        var notProcessed = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : Json("""{"RequestStatus": false, "ResponseMessage": "Insufficient balance", "isBvnValid": false}"""));
        var result = await Provider(notProcessed).LookupAsync("21111111111", "Ada", null, "Obi", null);
        Assert.Equal(BvnLookupOutcome.Unavailable, result.Outcome);
        Assert.Equal("Insufficient balance", result.Error);
    }

    [Fact]
    public async Task An_expired_session_answered_with_400_is_replaced_by_logging_in_again()
    {
        var logins = 0;
        var firstSessionQueries = 0;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/initialisation/init")
            {
                logins++;
                return Json(LoginJson);
            }

            // The first session works once, then expires — and the gateway says so with a 400, not a 401.
            return logins == 1 && ++firstSessionQueries > 1
                ? Json(SystemErrorJson, HttpStatusCode.BadRequest)
                : Json(ValidBvnJson);
        });
        var provider = Provider(handler);

        Assert.Equal(BvnLookupOutcome.Found, (await provider.LookupAsync("21111111111", "Muideen", null, "Oladipupo", null)).Outcome);
        Assert.Equal(BvnLookupOutcome.Found, (await provider.LookupAsync("21111111111", "Muideen", null, "Oladipupo", null)).Outcome);
        Assert.Equal(2, logins);
    }

    [Fact]
    public async Task The_gateways_own_error_message_is_passed_on_and_the_fallback_used_only_when_it_gives_none()
    {
        var invalidBvn = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : Json("""{"RequestStatus": false, "ResponseMessage": "Valid BVN is required (11 digits)", "isBvnValid": false}""", HttpStatusCode.BadRequest));
        var result = await Provider(invalidBvn).LookupAsync("12345678901", "Ada", null, "Obi", null);
        Assert.Equal(BvnLookupOutcome.Unavailable, result.Outcome);
        Assert.Equal("Valid BVN is required (11 digits)", result.Error);

        // A fresh session's failure isn't retried.
        Assert.Single(invalidBvn.Requests, r => r.Path == "/v1/identity/get_bvn");

        var systemError = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : Json(SystemErrorJson, HttpStatusCode.BadRequest));
        Assert.Equal("System Error! Contact your system administrator", (await Provider(systemError).LookupAsync("12345678901", "Ada", null, "Obi", null)).Error);

        var emptyBody400 = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : new HttpResponseMessage(HttpStatusCode.BadRequest));
        Assert.Equal(BcKashGatewayBvnVerificationProvider.ProviderError, (await Provider(emptyBody400).LookupAsync("12345678901", "Ada", null, "Obi", null)).Error);
    }

    [Fact]
    public async Task A_gateway_outage_stays_unavailable()
    {
        var outage = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var result = await Provider(outage).LookupAsync("21111111111", "Ada", null, "Obi", null);
        Assert.Equal(BvnLookupOutcome.Unavailable, result.Outcome);
        Assert.Equal(BcKashGatewayBvnVerificationProvider.ProviderError, result.Error);
    }

    private const string SystemErrorJson = """
        {"RequestStatus": false, "ResponseMessage": "System Error! Contact your system administrator", "isBvnValid": false}
        """;

    // A fresh account per provider, so the login cached for one test is never reused by another.
    private static BcKashGatewayBvnVerificationProvider Provider(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new BvnGatewaySettings { BaseUrl = "https://gateway.test/v1", Email = $"{Guid.NewGuid():N}@bckash.test", Password = "secret" }),
        NullLogger<BcKashGatewayBvnVerificationProvider>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record RecordedRequest(HttpMethod Method, string Path, Dictionary<string, string> Headers, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, headers, body));
            return respond(request);
        }
    }
}
