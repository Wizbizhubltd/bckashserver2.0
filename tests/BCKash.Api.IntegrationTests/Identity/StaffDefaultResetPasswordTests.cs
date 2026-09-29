using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>
/// A bulk forced reset hands out StaffPortal:DefaultResetPassword when it's 8 characters. Its own
/// fixture, so the configured host is the only one on its test database.
/// </summary>
public class StaffDefaultResetPasswordTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string DefaultPassword = "Bck@2026";

    private readonly WebApplicationFactory<Program> _factory;

    public StaffDefaultResetPasswordTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("StaffPortal:DefaultResetPassword", DefaultPassword));
    }

    [Fact]
    public async Task Forced_reset_gives_the_configured_default_password()
    {
        Assert.Equal(DefaultPassword, await ResetAndReadPasswordAsync("good"));
    }

    private async Task<string> ResetAndReadPasswordAsync(string suffix)
    {
        int staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            await TestDataSeeder.SeedUserAsync(db, $"bulk-default-admin-{suffix}@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            staffId = (await TestDataSeeder.SeedTypedUserAsync(db, $"bulk-default-{suffix}@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;
        }

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, $"bulk-default-admin-{suffix}@bckash.test", "Correct-Password1!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await client.PostAsJsonAsync("/api/v1/users/bulk/reset-password", new { userIds = new[] { staffId } }, TestJson.Options)).EnsureSuccessStatusCode();

        var emails = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var body = emails.Sent.Last(e => e.ToAddress == $"bulk-default-{suffix}@bckash.test").Body;
        return body.Split('\n').Single(l => l.StartsWith("Temporary password: ")).Replace("Temporary password: ", string.Empty).Trim();
    }
}

/// <summary>A configured default that isn't 8 characters is ignored in favour of a random 8-character password.</summary>
public class StaffInvalidDefaultResetPasswordTests : IClassFixture<BCKashWebApplicationFactory>
{
    private const string TooLong = "too-long-password";

    private readonly WebApplicationFactory<Program> _factory;

    public StaffInvalidDefaultResetPasswordTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("StaffPortal:DefaultResetPassword", TooLong));
    }

    [Fact]
    public async Task Forced_reset_falls_back_to_a_random_8_character_password()
    {
        int staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            await TestDataSeeder.SeedUserAsync(db, "bulk-default-admin-bad@bckash.test", "Correct-Password1!", permissionSlug: "users.manage");
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            staffId = (await TestDataSeeder.SeedTypedUserAsync(db, "bulk-default-bad@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;
        }

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, "bulk-default-admin-bad@bckash.test", "Correct-Password1!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await client.PostAsJsonAsync("/api/v1/users/bulk/reset-password", new { userIds = new[] { staffId } }, TestJson.Options)).EnsureSuccessStatusCode();

        var emails = (RecordingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var body = emails.Sent.Last(e => e.ToAddress == "bulk-default-bad@bckash.test").Body;
        var password = body.Split('\n').Single(l => l.StartsWith("Temporary password: ")).Replace("Temporary password: ", string.Empty).Trim();
        Assert.NotEqual(TooLong, password);
        Assert.Equal(8, password.Length);
    }
}
