using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

public class GlClosuresControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GlClosuresControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> CreateAccountAsync(HttpClient client, string glCode) =>
        (await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest(glCode, null, glCode, GlAccountType.Asset, true, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options))!.Id;

    /// <summary>Each test gets its own office so their closures (scoped per-office, like production) can't interfere with each other via the shared per-class database.</summary>
    private async Task<int> CreateOfficeAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var office = new Office { Name = name };
        db.Offices.Add(office);
        await db.SaveChangesAsync();
        return office.Id;
    }

    [Fact]
    public async Task Closing_a_period_blocks_a_same_or_earlier_dated_posting_and_reopening_unblocks_it()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-closure@bckash.test", ["gl.manage", "gl.closure-reopen"]);
        var officeId = await CreateOfficeAsync("Closure Test Office");
        var cashId = await CreateAccountAsync(client, "1000");
        var incomeId = await CreateAccountAsync(client, "4000");

        var closeResponse = await client.PostAsJsonAsync("/api/gl/closures", new CreateGlClosureRequest(officeId, new DateOnly(2026, 1, 31), "Month-end close"));
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closure = await closeResponse.Content.ReadFromJsonAsync<GlClosureResponse>(TestJson.Options);

        // A posting dated on/before the closure date is blocked.
        var blockedResponse = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            officeId, new DateOnly(2026, 1, 20),
            [new JournalEntryLineRequest(cashId, 100m, null), new JournalEntryLineRequest(incomeId, null, 100m)],
            "Backdated"));
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);

        // A posting dated after the closure still succeeds.
        var allowedResponse = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            officeId, new DateOnly(2026, 2, 5),
            [new JournalEntryLineRequest(cashId, 100m, null), new JournalEntryLineRequest(incomeId, null, 100m)],
            "After close"));
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);

        var reopenResponse = await client.PostAsJsonAsync($"/api/gl/closures/{closure!.Id}/reopen", new ReopenGlClosureRequest("Correcting an entry"));
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var reopened = await reopenResponse.Content.ReadFromJsonAsync<GlClosureResponse>(TestJson.Options);
        Assert.NotNull(reopened!.ReopenedAt);

        // Now the previously-blocked backdated posting succeeds.
        var nowAllowedResponse = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            officeId, new DateOnly(2026, 1, 20),
            [new JournalEntryLineRequest(cashId, 100m, null), new JournalEntryLineRequest(incomeId, null, 100m)],
            "Backdated, now allowed"));
        Assert.Equal(HttpStatusCode.OK, nowAllowedResponse.StatusCode);
    }

    [Fact]
    public async Task Reopening_without_the_elevated_permission_is_forbidden()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-closure-no-elevated@bckash.test", "gl.manage");
        var officeId = await CreateOfficeAsync("No Elevated Permission Office");

        var closeResponse = await client.PostAsJsonAsync("/api/gl/closures", new CreateGlClosureRequest(officeId, new DateOnly(2026, 3, 31), null));
        var closure = await closeResponse.Content.ReadFromJsonAsync<GlClosureResponse>(TestJson.Options);

        var reopenResponse = await client.PostAsJsonAsync($"/api/gl/closures/{closure!.Id}/reopen", new ReopenGlClosureRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, reopenResponse.StatusCode);
    }
}
