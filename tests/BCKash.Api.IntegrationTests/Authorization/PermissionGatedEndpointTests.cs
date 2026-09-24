using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Authorization;

/// <summary>
/// Exercises the dummy permission-gated endpoint (Settings create, guarded by
/// "Permission:settings.manage") required by the Phase 0 acceptance criteria: a user
/// whose role carries the permission gets in, one whose role doesn't gets 403.
/// </summary>
public class PermissionGatedEndpointTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public PermissionGatedEndpointTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_with_the_permission_can_create_a_setting()
    {
        var token = await LoginAndGetTokenAsync("perm-allowed@bckash.test", grantPermission: true);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest("test.key", "test-value"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task User_without_the_permission_is_forbidden()
    {
        var token = await LoginAndGetTokenAsync("perm-denied@bckash.test", grantPermission: false);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest("test.key", "test-value"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> LoginAndGetTokenAsync(string email, bool grantPermission)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        await TestDataSeeder.SeedUserAsync(db, email, "Correct-Password1!", permissionSlug: grantPermission ? "settings.manage" : null);

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, email, "Correct-Password1!");
        return tokens.AccessToken;
    }
}
