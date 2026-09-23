using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Audit;

/// <summary>
/// Proves the audit interceptor (FR-SEC-6) writes a correct audit_trail row for every
/// create/update/delete on a dummy audited entity, with the acting user attached.
/// </summary>
public class AuditTrailTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public AuditTrailTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_update_delete_of_a_setting_each_produce_an_audit_trail_row()
    {
        using var seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(seedDb, "audit-actor@bckash.test", "Correct-Password1!", permissionSlug: "settings.manage");

        using var client = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, client, user.Email, "Correct-Password1!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/settings", new CreateSettingRequest("audit.test.key", "v1"));
        var created = await createResponse.Content.ReadFromJsonAsync<SettingResponse>();

        await client.PutAsJsonAsync($"/api/settings/{created!.Id}", new UpdateSettingRequest("v2"));
        await client.DeleteAsync($"/api/settings/{created.Id}");

        using var assertScope = _factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var entries = await db.AuditTrail
            .Where(a => a.Module == "Setting" && a.UserId == user.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(["Create", "Update", "Delete"], entries.Select(e => e.Action));
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Notes)));
    }
}
