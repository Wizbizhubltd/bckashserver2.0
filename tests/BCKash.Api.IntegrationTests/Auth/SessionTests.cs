using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

public class SessionTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SessionTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Signing_in_on_a_second_device_signs_the_first_one_out()
    {
        const string email = "session-two-devices@bckash.test";
        await SeedAsync(email);

        var (laptop, laptopTokens) = await SignInAsync(email, "laptop-device-id");
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/v1/offices")).StatusCode);

        var (phone, _) = await SignInAsync(email, "phone-device-id");

        var laptopAfter = await laptop.GetAsync("/api/v1/offices");
        Assert.Equal(HttpStatusCode.Unauthorized, laptopAfter.StatusCode);
        Assert.Equal("session_replaced", await ReasonAsync(laptopAfter));
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/v1/offices")).StatusCode);

        // The first device can't get back in with its refresh token either.
        var laptopRefresh = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(laptopTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, laptopRefresh.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.SingleAsync(u => u.Email == email);
        Assert.Equal("phone-device-id", user.ActiveDeviceId);
    }

    [Fact]
    public async Task Refreshing_keeps_the_current_device_signed_in()
    {
        const string email = "session-refresh@bckash.test";
        await SeedAsync(email);
        var (_, tokens) = await SignInAsync(email, "only-device");

        var refresh = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<TokenResponse>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/offices")).StatusCode);
    }

    [Fact]
    public async Task New_staff_must_change_their_temporary_password_before_doing_anything_else()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "pwd-change-admin@bckash.test");
        var created = await superAdmin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            "pwd-change-staff@bckash.test", "New", "Staff", null, null, "manager", UserClass.Initiator, null, null, null));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());

        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var credentialsEmail = emailSender.Sent.Last(e => e.ToAddress == "pwd-change-staff@bckash.test");
        var temporaryPassword = Regex.Match(credentialsEmail.Body, @"Temporary password: (\S+)").Groups[1].Value;

        var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, "pwd-change-staff@bckash.test", temporaryPassword);
        Assert.True(tokens.UserData.MustChangePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var blocked = await client.GetAsync("/api/v1/offices");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password_change_required", await ReasonAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users/me")).StatusCode);

        var wrongCurrent = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest("not-it", "Brand-New-Password1!"));
        var tooShort = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(temporaryPassword, "short"));
        var unchanged = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(temporaryPassword, temporaryPassword));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);

        var changed = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(temporaryPassword, "Brand-New-Password1!"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var newTokens = await changed.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.False(newTokens!.UserData.MustChangePassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/offices")).StatusCode);
    }

    [Fact]
    public async Task A_new_super_admin_is_not_asked_to_change_their_password()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "pwd-change-admin-2@bckash.test");
        var created = await superAdmin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            "pwd-change-new-admin@bckash.test", "New", "Admin", null, null, "super_admin", UserClass.Initiator, null, null, null));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().Users.SingleAsync(u => u.Email == "pwd-change-new-admin@bckash.test");
        Assert.False(user.MustChangePassword);
    }

    private async Task SeedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedUserAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>(), email, "Correct-Password1!", permissionSlug: "organization.manage");
    }

    private async Task<(HttpClient Client, OtpVerifyResponse Tokens)> SignInAsync(string email, string deviceId)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Password1!"));
        var challenge = await login.Content.ReadFromJsonAsync<OtpChallengeResponse>();

        var emailSender = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var otpEmail = emailSender.Sent.Last(e => e.ToAddress == email);
        var code = Regex.Match(otpEmail.Body, @"verification code is (\d{6})").Groups[1].Value;

        var verify = await client.PostAsJsonAsync("/api/v1/auth/login/otp/verify", new OtpVerifyRequest(challenge!.ChallengeToken, code, deviceId));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<OtpVerifyResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    private static async Task<string?> ReasonAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
    }
}
