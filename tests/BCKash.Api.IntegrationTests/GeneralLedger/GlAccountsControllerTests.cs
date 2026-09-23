using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.GeneralLedger;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

public class GlAccountsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GlAccountsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_and_fetch_a_gl_account()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-account-crud@bckash.test", "gl.manage");

        var response = await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Cash", null, "1000", GlAccountType.Asset, true, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        Assert.Equal("Cash", created!.Name);
        Assert.Equal(GlAccountType.Asset, created.AccountType);
    }

    [Fact]
    public async Task Updating_an_accounts_parent_to_its_own_descendant_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-account-cycle@bckash.test", "gl.manage");

        var parentResponse = await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Assets", null, "1", GlAccountType.Asset, true, null));
        var parent = await parentResponse.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);

        var childResponse = await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Cash", parent!.Id, "1000", GlAccountType.Asset, true, null));
        var child = await childResponse.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);

        var cycleResponse = await client.PutAsJsonAsync($"/api/gl-accounts/{parent.Id}",
            new SaveGlAccountRequest("Assets", child!.Id, "1", GlAccountType.Asset, true, null));

        Assert.Equal(HttpStatusCode.BadRequest, cycleResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivate_and_reactivate_an_account()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-account-deactivate@bckash.test", "gl.manage");

        var created = await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Suspense", null, "9999", GlAccountType.Liability, true, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);

        var deactivated = await (await client.PostAsync($"/api/gl-accounts/{created!.Id}/deactivate", null)).Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        Assert.False(deactivated!.Active);

        var reactivated = await (await client.PostAsync($"/api/gl-accounts/{created.Id}/activate", null)).Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        Assert.True(reactivated!.Active);
    }
}
