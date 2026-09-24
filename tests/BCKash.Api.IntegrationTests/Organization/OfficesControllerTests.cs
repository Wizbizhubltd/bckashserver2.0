using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

public class OfficesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public OfficesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_and_fetch_an_office()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-crud@bckash.test", "organization.manage");

        var response = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Head Office", null, true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OfficeResponse>();
        Assert.Equal("Head Office", created!.Name);
    }

    [Fact]
    public async Task Updating_an_offices_parent_to_its_own_descendant_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-cycle@bckash.test", "organization.manage");

        var parentResponse = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Parent", null, false));
        var parent = await parentResponse.Content.ReadFromJsonAsync<OfficeResponse>();

        var childResponse = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Child", parent!.Id, false));
        var child = await childResponse.Content.ReadFromJsonAsync<OfficeResponse>();

        // Try to make Parent a child of its own child — a direct cycle.
        var cycleResponse = await client.PutAsJsonAsync($"/api/v1/offices/{parent.Id}",
            await OfficeRequestAsync("Parent", child!.Id, false));

        Assert.Equal(HttpStatusCode.BadRequest, cycleResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivating_an_office_with_an_active_client_requires_confirmation()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-deactivate@bckash.test", "organization.manage");

        var officeResponse = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Busy Branch", null, false));
        var office = await officeResponse.Content.ReadFromJsonAsync<OfficeResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Clients.Add(new Client { OfficeId = office!.Id, Status = ClientStatus.Active, AccountNo = "C-1" });
            await db.SaveChangesAsync();
        }

        // Without confirm — blocked with counts.
        var blockedResponse = await client.PostAsync($"/api/v1/offices/{office!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);
        var counts = await blockedResponse.Content.ReadFromJsonAsync<OfficeInUseResponse>();
        Assert.Equal(1, counts!.ActiveClientCount);

        // With confirm=true — succeeds and is audited.
        var confirmedResponse = await client.PostAsync($"/api/v1/offices/{office.Id}/deactivate?confirm=true", null);
        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);
        var deactivated = await confirmedResponse.Content.ReadFromJsonAsync<OfficeResponse>();
        Assert.False(deactivated!.Active);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var auditedUpdate = await assertDb.AuditTrail
            .Where(a => a.Module == "Office" && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(auditedUpdate);
    }

    [Fact]
    public async Task Deactivating_an_office_with_an_open_loan_requires_confirmation()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-deactivate-loan@bckash.test", "organization.manage");

        var officeResponse = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Loan Branch", null, false));
        var office = await officeResponse.Content.ReadFromJsonAsync<OfficeResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.Add(new Loan { OfficeId = office!.Id, Status = LoanStatus.Disbursed });
            await db.SaveChangesAsync();
        }

        var blockedResponse = await client.PostAsync($"/api/v1/offices/{office!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);
        var counts = await blockedResponse.Content.ReadFromJsonAsync<OfficeInUseResponse>();
        Assert.Equal(1, counts!.OpenLoanCount);
    }

    [Fact]
    public async Task Deactivating_an_office_with_no_active_relationships_needs_no_confirmation()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "office-deactivate-empty@bckash.test", "organization.manage");

        var officeResponse = await client.PostAsJsonAsync("/api/v1/offices", await OfficeRequestAsync("Quiet Branch", null, false));
        var office = await officeResponse.Content.ReadFromJsonAsync<OfficeResponse>();

        var response = await client.PostAsync($"/api/v1/offices/{office!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<SaveOfficeRequest> OfficeRequestAsync(string name, int? parentId, bool defaultOffice)
    {
        using var scope = _factory.Services.CreateScope();
        var location = await TestDataSeeder.SeedOfficeLocationAsync(scope.ServiceProvider.GetRequiredService<BCKashDbContext>());
        return new SaveOfficeRequest(
            name, parentId, null, null, null, null, null, null, null, defaultOffice,
            location.StateId, location.LgaId, location.CityId, location.ZoneId);
    }
}
