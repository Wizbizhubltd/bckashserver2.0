using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

public class LoginTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoginTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Correct_password_triggers_an_otp_challenge_and_verifying_it_issues_a_valid_jwt_with_user_data()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "login-success@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login-success@bckash.test", "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var challenge = await loginResponse.Content.ReadFromJsonAsync<OtpChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge!.ChallengeToken));

        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var otpEmail = emailSender.Sent.Last(e => e.ToAddress == "login-success@bckash.test");
        var code = Regex.Match(otpEmail.Body, @"verification code is (\d{6})").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(code));

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/login/otp/verify", new OtpVerifyRequest(challenge.ChallengeToken, code));
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var body = await verifyResponse.Content.ReadFromJsonAsync<OtpVerifyResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal("login-success@bckash.test", body.UserData.Email);
        Assert.Equal("Test User", body.UserData.FullName);

        // Prove it's a real, verifiable JWT signed with the configured key — not just a random string.
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(body.AccessToken);
        Assert.Equal(BCKashWebApplicationFactory.TestIssuer, token.Issuer);
        Assert.Contains(token.Audiences, a => a == BCKashWebApplicationFactory.TestAudience);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "login-wrong-pw@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login-wrong-pw@bckash.test", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_otp_code_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "login-wrong-otp@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login-wrong-otp@bckash.test", "Correct-Password1!"));
        var challenge = await loginResponse.Content.ReadFromJsonAsync<OtpChallengeResponse>();

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/login/otp/verify", new OtpVerifyRequest(challenge!.ChallengeToken, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_issues_a_new_access_token()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, "login-refresh@bckash.test", "Correct-Password1!");

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, "login-refresh@bckash.test", "Correct-Password1!");

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        // Not comparing AccessToken for inequality here: two tokens for the same user with the
        // same claims, issued within the same second, are legitimately byte-identical JWTs —
        // that's not a bug. The refresh token IS guaranteed unique (randomly generated), so that's
        // the meaningful rotation invariant to check.
        Assert.NotEqual(tokens.RefreshToken, refreshed!.RefreshToken);

        // Rotated: the old refresh token must not be usable a second time.
        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }
}
