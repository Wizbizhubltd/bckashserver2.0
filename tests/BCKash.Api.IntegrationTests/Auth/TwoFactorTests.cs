using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

public class TwoFactorTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;
    private const string Secret = "JBSWY3DPEHPK3PXP"; // fixed base32 test secret

    public TwoFactorTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_with_2fa_enabled_requires_a_second_step_then_succeeds_with_the_right_code()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "2fa-user@bckash.test", "Correct-Password1!", enableGoogle2fa: true, google2faSecret: Secret);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("2fa-user@bckash.test", "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var challenge = await loginResponse.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge!.ChallengeToken));

        var validCode = new Totp(Base32Encoding.ToBytes(Secret)).ComputeTotp();
        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/login/2fa", new TwoFactorRequest(challenge.ChallengeToken, validCode));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var tokens = await verifyResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
    }

    [Fact]
    public async Task Wrong_totp_code_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "2fa-wrong-code@bckash.test", "Correct-Password1!", enableGoogle2fa: true, google2faSecret: Secret);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("2fa-wrong-code@bckash.test", "Correct-Password1!"));
        var challenge = await loginResponse.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/login/2fa", new TwoFactorRequest(challenge!.ChallengeToken, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
    }
}
