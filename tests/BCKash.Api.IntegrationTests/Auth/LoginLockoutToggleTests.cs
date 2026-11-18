using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Auth;

/// <summary>Settings → Roles &amp; Access → "Lock accounts after failed sign-ins" (off by default).</summary>
public class LoginLockoutToggleTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoginLockoutToggleTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task With_the_lockout_off_any_number_of_wrong_passwords_never_locks_the_account_and_a_super_admin_can_switch_it_on()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "lockout-off@bckash.test", "Correct-Password1!");
        using var client = _factory.CreateClient();

        // Far past the factory's limit of 3.
        for (var i = 0; i < 8; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var correct = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);

        // A super admin switches it on — only "0" or "1" is accepted — and it applies at once.
        var admin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "lockout-admin@bckash.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(LoginLockoutRules.SettingKey, "yes"))).StatusCode);
        var saved = await admin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(LoginLockoutRules.SettingKey, "1"));
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "wrong-password"));
        }

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Correct-Password1!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task A_temporary_password_copied_with_a_trailing_space_still_signs_in()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "lockout-trailing-space@bckash.test", "Temp-Pass9!");
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "Temp-Pass9! "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
