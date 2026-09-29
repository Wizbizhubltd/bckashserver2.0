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
        Assert.Equal(BvnLookupOutcome.Unavailable, (await Provider(refusedLogin).LookupAsync("21111111111", "Ada", null, "Obi", null)).Outcome);

        var notProcessed = new StubHandler(request => request.RequestUri!.AbsolutePath == "/v1/initialisation/init"
            ? Json(LoginJson)
            : Json("""{"RequestStatus": false, "ResponseMessage": "Insufficient balance", "isBvnValid": false}"""));
        var result = await Provider(notProcessed).LookupAsync("21111111111", "Ada", null, "Obi", null);
        Assert.Equal(BvnLookupOutcome.Unavailable, result.Outcome);
        Assert.Equal("Insufficient balance", result.Error);
    }

    // A fresh account per provider, so the login cached for one test is never reused by another.
    private static BcKashGatewayBvnVerificationProvider Provider(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new BvnGatewaySettings { BaseUrl = "https://gateway.test/v1", Email = $"{Guid.NewGuid():N}@bckash.test", Password = "secret" }),
        NullLogger<BcKashGatewayBvnVerificationProvider>.Instance);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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
