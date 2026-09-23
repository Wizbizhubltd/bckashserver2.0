using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.GeneralLedger;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

public class GlJournalEntriesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GlJournalEntriesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<(int CashId, int IncomeId)> CreateAccountsAsync(HttpClient client, bool cashManualEntries = true)
    {
        var cash = await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Cash", null, "1000", GlAccountType.Asset, cashManualEntries, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        var income = await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Other Income", null, "4000", GlAccountType.Income, true, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return (cash!.Id, income!.Id);
    }

    [Fact]
    public async Task Create_approve_then_reverse_a_manual_journal_entry()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-manual-entry@bckash.test", "gl.manage");
        var (cashId, incomeId) = await CreateAccountsAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            null, new DateOnly(2026, 1, 15),
            [new JournalEntryLineRequest(cashId, 500m, null), new JournalEntryLineRequest(incomeId, null, 500m)],
            "Sundry income"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<List<GlJournalEntryResponse>>(TestJson.Options);
        Assert.Equal(2, created!.Count);
        Assert.All(created, e => Assert.False(e.Approved));
        var reference = created[0].Reference!;

        var approveResponse = await client.PostAsJsonAsync($"/api/gl/journal-entries/{reference}/approve", new ApproveJournalEntryRequest("Looks right"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<List<GlJournalEntryResponse>>(TestJson.Options);
        Assert.All(approved!, e => Assert.True(e.Approved));

        var reverseResponse = await client.PostAsync($"/api/gl/journal-entries/{reference}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversed = await reverseResponse.Content.ReadFromJsonAsync<List<GlJournalEntryResponse>>(TestJson.Options);
        Assert.All(reversed!, e => Assert.True(e.Reversed));
    }

    [Fact]
    public async Task Unbalanced_lines_are_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-unbalanced@bckash.test", "gl.manage");
        var (cashId, incomeId) = await CreateAccountsAsync(client);

        var response = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            null, new DateOnly(2026, 1, 15),
            [new JournalEntryLineRequest(cashId, 500m, null), new JournalEntryLineRequest(incomeId, null, 400m)],
            "Mismatched"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Posting_to_an_account_with_manual_entries_disabled_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "gl-manual-disabled@bckash.test", "gl.manage");
        var (cashId, incomeId) = await CreateAccountsAsync(client, cashManualEntries: false);

        var response = await client.PostAsJsonAsync("/api/gl/journal-entries", new CreateManualJournalEntryRequest(
            null, new DateOnly(2026, 1, 15),
            [new JournalEntryLineRequest(cashId, 500m, null), new JournalEntryLineRequest(incomeId, null, 500m)],
            "Should be blocked"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
