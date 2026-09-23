using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BCKash.Api.IntegrationTests;

/// <summary>Seeds a user with the given permission and returns an HttpClient carrying its bearer token.</summary>
public static class AuthenticatedClientFactory
{
    public static Task<HttpClient> CreateAsync(BCKashWebApplicationFactory factory, string email, string permissionSlug) =>
        CreateAsync(factory, email, [permissionSlug]);

    /// <summary>For a test that needs to act across more than one permission-gated module in the same request flow (e.g. creating a client while exercising a group-membership endpoint).</summary>
    public static async Task<HttpClient> CreateAsync(BCKashWebApplicationFactory factory, string email, IEnumerable<string> permissionSlugs)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var slugs = permissionSlugs.ToList();
        var user = await TestDataSeeder.SeedUserAsync(db, email, "Correct-Password1!", permissionSlug: slugs[0], additionalPermissionSlugs: slugs.Skip(1));

        var client = factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(factory, client, user.Email, "Correct-Password1!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }
}
