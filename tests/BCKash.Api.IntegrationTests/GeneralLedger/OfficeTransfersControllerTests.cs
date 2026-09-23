using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

public class OfficeTransfersControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public OfficeTransfersControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Creating_a_transfer_posts_a_balanced_matched_pair()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-transfer@bckash.test", "gl.manage");

        int fromOfficeId, toOfficeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var from = new Office { Name = "From Branch" };
            var to = new Office { Name = "To Branch" };
            db.Offices.AddRange(from, to);
            await db.SaveChangesAsync();
            fromOfficeId = from.Id;
            toOfficeId = to.Id;
        }

        var cashAccount = await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Cash in Transit", null, "1900", GlAccountType.Asset, true, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync("/api/gl/office-transfers", new CreateOfficeTransferRequest(
            fromOfficeId, toOfficeId, null, 2500m, cashAccount!.Id, new DateOnly(2026, 1, 10), "Branch funding"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<OfficeTransactionResponse>(TestJson.Options);
        Assert.Equal(2500m, transfer!.Amount);

        var entries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/gl/journal-entries?reference=TRANSFER-{transfer.Id}", TestJson.Options);
        Assert.Equal(2, entries!.Count);
        Assert.Equal(entries.Sum(e => e.Debit ?? 0m), entries.Sum(e => e.Credit ?? 0m));
        Assert.Contains(entries, e => e.OfficeId == fromOfficeId && e.Credit == 2500m);
        Assert.Contains(entries, e => e.OfficeId == toOfficeId && e.Debit == 2500m);
    }

    [Fact]
    public async Task Transferring_to_the_same_office_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-transfer-same@bckash.test", "gl.manage");

        int officeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = new Office { Name = "Only Branch" };
            db.Offices.Add(office);
            await db.SaveChangesAsync();
            officeId = office.Id;
        }

        var cashAccount = await (await client.PostAsJsonAsync("/api/gl-accounts", new SaveGlAccountRequest("Cash", null, "1000", GlAccountType.Asset, true, null)))
            .Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync("/api/gl/office-transfers", new CreateOfficeTransferRequest(
            officeId, officeId, null, 100m, cashAccount!.Id, new DateOnly(2026, 1, 10), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
