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

public class PasswordResetTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public PasswordResetTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reset_code_sets_a_new_password_that_can_then_sign_in()
    {
        await SeedAsync("reset-success@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-success@bckash.test");
        var code = ReadResetCode("reset-success@bckash.test");

        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "New-Password1!"));
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("reset-success@bckash.test", "Old-Password1!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("reset-success@bckash.test", "New-Password1!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_code_cannot_be_used_twice()
    {
        await SeedAsync("reset-reuse@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-reuse@bckash.test");
        var code = ReadResetCode("reset-reuse@bckash.test");

        await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "New-Password1!"));
        var second = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "Another-Password1!"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Wrong_code_is_rejected()
    {
        await SeedAsync("reset-wrong-code@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-wrong-code@bckash.test");
        var response = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, "000000", "New-Password1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Short_password_is_rejected_without_consuming_the_code()
    {
        await SeedAsync("reset-weak@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-weak@bckash.test");
        var code = ReadResetCode("reset-weak@bckash.test");

        var weak = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var retry = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, code, "Long-Enough1!"));
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_gets_a_challenge_indistinguishable_from_a_real_one()
    {
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-nobody@bckash.test");
        var response = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, "123456", "New-Password1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_challenge_cannot_be_used_to_complete_a_login()
    {
        await SeedAsync("reset-no-login@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        var challenge = await RequestResetAsync(client, "reset-no-login@bckash.test");
        var code = ReadResetCode("reset-no-login@bckash.test");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(challenge, code));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Second_request_within_the_cooldown_is_rejected()
    {
        await SeedAsync("reset-cooldown@bckash.test", "Old-Password1!");
        using var client = _factory.CreateClient();

        await RequestResetAsync(client, "reset-cooldown@bckash.test");
        var second = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("reset-cooldown@bckash.test"));

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    private async Task SeedAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, email, password);
    }

    private static async Task<string> RequestResetAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        return body!.ChallengeToken;
    }

    private string ReadResetCode(string email)
    {
        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var resetEmail = emailSender.Sent.Last(e => e.ToAddress == email);
        return Regex.Match(resetEmail.Body, @"reset code is (\d{6})").Groups[1].Value;
    }
}
