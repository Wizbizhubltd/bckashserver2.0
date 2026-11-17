using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>
/// Settings → Customer Onboarding → "BVN verification mandatory": on by default, so a client can't be
/// onboarded without a BVN check; switched off, they can — unverified, with the details as entered —
/// and can be verified later. (Its own class, so the setting never leaks into other tests.)
/// </summary>
public class BvnVerificationOptionalTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string Password = "Correct-Password1!";

    private readonly BCKashWebApplicationFactory _factory;

    public BvnVerificationOptionalTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Bvn_verification_is_mandatory_until_switched_off_and_then_clients_onboard_unverified()
    {
        var (officeId, marketer) = await SetUpOfficeAsync();

        Task<HttpResponseMessage> Onboard(string bvn) =>
            marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
                null, new OnboardClientRequest("Ada Obi", null, "08031234567", bvn, null, null, null)));

        // Never saved — mandatory.
        Assert.True((await marketer.GetFromJsonAsync<OnboardingRulesResponse>("/api/v1/onboarding/rules", TestJson.Options))!.BvnVerificationRequired);
        Assert.Equal(HttpStatusCode.BadRequest, (await Onboard(NewBvn())).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Settings.Add(new Setting { SettingKey = BvnVerificationRules.SettingKey, SettingValue = "0" });
            await db.SaveChangesAsync();
        }

        Assert.False((await marketer.GetFromJsonAsync<OnboardingRulesResponse>("/api/v1/onboarding/rules", TestJson.Options))!.BvnVerificationRequired);

        var bvn = NewBvn();
        var response = await Onboard(bvn);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var onboarded = (await response.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single();
        Assert.False(onboarded.IsHighRisk);

        var client = await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{onboarded.Id}", TestJson.Options);
        Assert.Equal(officeId, client!.OfficeId);
        Assert.Equal(bvn, client.Bvn);
        Assert.Null(client.BvnVerifiedAt);
        Assert.Equal("Ada", client.FirstName);

        // The BVN still has to be well-formed and not already on the platform.
        Assert.Equal(HttpStatusCode.BadRequest, (await Onboard(bvn)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Onboard("123")).StatusCode);

        // A check that was run is still honoured — the client is saved verified.
        var verifiedBvn = NewBvn();
        var check = await marketer.PostAsJsonAsync("/api/v1/onboarding/bvn-check", new BvnCheckRequest(verifiedBvn, "Ada Obi", "08031234567"));
        var verificationId = (await check.Content.ReadFromJsonAsync<BvnCheckResponse>(TestJson.Options))!.VerificationId;
        var verified = await marketer.PostAsJsonAsync("/api/v1/onboarding/clients", new OnboardSingleClientRequest(
            null, new OnboardClientRequest("Ada Obi", null, "08031234567", verifiedBvn, verificationId, null, null)));
        Assert.Equal(HttpStatusCode.Created, verified.StatusCode);
        var verifiedId = (await verified.Content.ReadFromJsonAsync<OnboardingResponse>(TestJson.Options))!.Clients.Single().Id;
        Assert.NotNull((await marketer.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{verifiedId}", TestJson.Options))!.BvnVerifiedAt);
    }

    private async Task<(int OfficeId, HttpClient Marketer)> SetUpOfficeAsync()
    {
        int officeId;
        var marketerEmail = $"bvn-optional-marketer-{Guid.NewGuid():N}@bckash.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            officeId = (await TestDataSeeder.SeedOfficeAsync(db)).Id;
            await TestDataSeeder.SeedTypedUserAsync(db, marketerEmail, Password, UserTypeSlugs.Marketer, officeId);
        }

        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, marketerEmail, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (officeId, client);
    }

    private static string NewBvn() => $"{Random.Shared.Next(2, 9)}{Random.Shared.NextInt64(100_000_000, 999_999_999)}1";
}
