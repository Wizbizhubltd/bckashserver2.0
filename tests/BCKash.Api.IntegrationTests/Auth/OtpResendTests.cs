using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

public class OtpResendTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public OtpResendTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Resent_code_completes_the_login_and_the_old_code_no_longer_works()
    {
        const string email = "otp-resend-success@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var firstChallenge = await LoginAsync(client, email);
        var firstCode = ReadLoginCode(email);
        await ExpireCooldownAsync(email);

        var resendResponse = await client.PostAsJsonAsync("/api/v1/auth/login/otp/resend", new OtpResendRequest(firstChallenge));
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var secondChallenge = (await resendResponse.Content.ReadFromJsonAsync<OtpChallengeResponse>())!.ChallengeToken;
        var secondCode = ReadLoginCode(email);

        var oldVerify = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(firstChallenge, firstCode));
        Assert.Equal(HttpStatusCode.Unauthorized, oldVerify.StatusCode);

        var newVerify = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(secondChallenge, secondCode));
        Assert.Equal(HttpStatusCode.OK, newVerify.StatusCode);
    }

    [Fact]
    public async Task Resend_within_the_cooldown_is_rejected()
    {
        const string email = "otp-resend-cooldown@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var challenge = await LoginAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login/otp/resend", new OtpResendRequest(challenge));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Resend_after_the_login_completed_is_rejected()
    {
        const string email = "otp-resend-consumed@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var challenge = await LoginAsync(client, email);
        await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(challenge, ReadLoginCode(email)));
        await ExpireCooldownAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login/otp/resend", new OtpResendRequest(challenge));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_challenge_cannot_be_used_to_resend_a_login_code()
    {
        const string email = "otp-resend-reset-token@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var forgotResponse = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        var resetChallenge = (await forgotResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>())!.ChallengeToken;
        await ExpireCooldownAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login/otp/resend", new OtpResendRequest(resetChallenge));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SeedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, email, "Correct-Password1!");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OtpChallengeResponse>())!.ChallengeToken;
    }

    private string ReadLoginCode(string email)
    {
        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var otpEmail = emailSender.Sent.Last(e => e.ToAddress == email);
        return Regex.Match(otpEmail.Body, @"verification code is (\d{6})").Groups[1].Value;
    }

    // Backdates this user's codes past the 60-second resend cooldown instead of waiting it out.
    private async Task ExpireCooldownAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var otps = await db.LoginOtps.Where(o => o.User.Email == email).ToListAsync();
        foreach (var otp in otps)
        {
            otp.CreatedAt = otp.CreatedAt.AddMinutes(-2);
        }

        await db.SaveChangesAsync();
    }
}
