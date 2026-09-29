using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

/// <summary>What a super admin can do from the control portal's client and group pages.</summary>
public class SuperAdminClientActionsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SuperAdminClientActionsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedClientAsync(ClientStatus status, DateOnly? activatedDate = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var office = await TestDataSeeder.SeedOfficeAsync(db);
        var client = new Client { FirstName = "Super", LastName = "Admin Case", OfficeId = office.Id, Status = status, ActivatedDate = activatedDate, CreatedAt = DateTime.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        await TestDataSeeder.SeedApprovalRequirementsAsync(db, client.Id);
        return client.Id;
    }

    [Fact]
    public async Task A_super_admin_can_approve_a_pending_client()
    {
        var http = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sa-approve@bckash.test");
        var clientId = await SeedClientAsync(ClientStatus.Pending);

        var detail = await http.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.True(detail!.Actions!.CanApprove);

        var response = await http.PostAsJsonAsync($"/api/v1/clients/{clientId}/activate", new { activatedDate = (DateOnly?)null }, TestJson.Options);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var verify = _factory.Services.CreateScope();
        Assert.Equal(ClientStatus.Active, (await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Clients.SingleAsync(c => c.Id == clientId)).Status);
    }

    [Fact]
    public async Task A_super_admin_can_grant_an_edit_request()
    {
        var http = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sa-grant-edit@bckash.test");
        var clientId = await SeedClientAsync(ClientStatus.Active, DateOnly.FromDateTime(DateTime.UtcNow));
        int requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var request = new ClientEditRequest { ClientId = clientId, Reason = "Wrong address", CreatedAt = DateTime.UtcNow };
            db.ClientEditRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
        }

        var detail = await http.GetFromJsonAsync<ClientResponse>($"/api/v1/clients/{clientId}", TestJson.Options);
        Assert.True(detail!.Actions!.CanReviewEditRequests);

        var response = await http.PostAsJsonAsync($"/api/v1/client-edit-requests/{requestId}/approve", new { note = "Go ahead" }, TestJson.Options);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var verify = _factory.Services.CreateScope();
        Assert.Equal(requestId, (await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Clients.SingleAsync(c => c.Id == clientId)).EditPrivilegeRequestId);
    }

    [Fact]
    public async Task Deletion_requests_can_be_looked_up_for_one_client()
    {
        var http = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sa-deletion-lookup@bckash.test");
        var clientId = await SeedClientAsync(ClientStatus.Active, DateOnly.FromDateTime(DateTime.UtcNow));
        var otherClientId = await SeedClientAsync(ClientStatus.Active, DateOnly.FromDateTime(DateTime.UtcNow));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.DeletionRequests.AddRange(
                new DeletionRequest { EntityType = DeletionRequest.ClientEntity, EntityId = clientId, Reason = "Duplicate", CreatedAt = DateTime.UtcNow },
                new DeletionRequest { EntityType = DeletionRequest.ClientEntity, EntityId = otherClientId, Reason = "Duplicate", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var page = await http.GetFromJsonAsync<PagedResult<DeletionRequestResponse>>(
            $"/api/v1/deletion-requests?status=Pending&entityType=client&entityId={clientId}", TestJson.Options);

        Assert.Equal(clientId, Assert.Single(page!.Items).EntityId);
    }

    [Fact]
    public async Task A_super_admin_can_delete_a_never_approved_group()
    {
        var http = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "sa-delete-group@bckash.test");
        int groupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var group = new Group { Name = "Never approved", OfficeId = office.Id, Status = GroupStatus.Pending, CreatedAt = DateTime.UtcNow };
            db.Groups.Add(group);
            await db.SaveChangesAsync();
            groupId = group.Id;
        }

        var summary = await http.GetFromJsonAsync<GroupSummaryResponse>($"/api/v1/groups/{groupId}/summary", TestJson.Options);
        Assert.True(summary!.CanDelete);

        var response = await http.DeleteAsync($"/api/v1/groups/{groupId}");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
