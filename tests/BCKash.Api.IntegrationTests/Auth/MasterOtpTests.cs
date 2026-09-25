using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

public class MasterOtpTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string MasterOtp = "424242";
    private const string Password = "Correct-Password1!";

    private readonly WebApplicationFactory<Program> _factory;

    public MasterOtpTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("MASTER_OTP", MasterOtp));
    }

    [Fact]
    public async Task Master_otp_completes_the_login()
    {
        const string email = "master-otp-login@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var challenge = await LoginAsync(client, email);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(challenge, MasterOtp));

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task Other_wrong_codes_are_still_rejected_when_a_master_otp_is_set()
    {
        const string email = "master-otp-wrong@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var challenge = await LoginAsync(client, email);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(challenge, "111111"));

        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task Master_otp_completes_a_password_reset()
    {
        const string email = "master-otp-reset@bckash.test";
        await SeedAsync(email);
        using var client = _factory.CreateClient();

        var forgotResponse = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        var challenge = (await forgotResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>())!.ChallengeToken;
        var reset = await client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(challenge, MasterOtp, "New-Password1!"));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    private async Task SeedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, email, Password);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OtpChallengeResponse>())!.ChallengeToken;
    }
}
