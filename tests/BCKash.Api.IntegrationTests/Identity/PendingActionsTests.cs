using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Identity;

/// <summary>The super admin's pending actions: requests waiting on approval, read live from the records.</summary>
public class PendingActionsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public PendingActionsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Pending_requests_are_listed_and_drop_off_once_dealt_with()
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "pending-actions-sa@bckash.test");
        int deletionId, highRiskId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var risky = new Client { FirstName = "Risky", LastName = "Client", OfficeId = office.Id, Status = ClientStatus.Pending, IsHighRisk = true, HighRiskReason = "Name differs", CreatedAt = DateTime.UtcNow };
            db.Clients.Add(risky);
            await db.SaveChangesAsync();
            var deletion = new DeletionRequest
            {
                EntityType = DeletionRequest.ClientEntity,
                EntityId = risky.Id,
                EntityName = "Risky Client",
                OfficeId = office.Id,
                Reason = "Duplicate",
                Status = DeletionRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            db.DeletionRequests.Add(deletion);
            await db.SaveChangesAsync();
            (deletionId, highRiskId) = (deletion.Id, risky.Id);
        }

        var pending = await superAdmin.GetFromJsonAsync<PendingActionsResponse>("/api/v1/pending-actions", TestJson.Options);
        Assert.Contains(pending!.Groups.Single(g => g.Key == "deletion_requests").Items, i => i.Id == deletionId);
        Assert.Contains(pending.Groups.Single(g => g.Key == "high_risk_clients").Items, i => i.Id == highRiskId && i.Link == $"/clients/{highRiskId}");
        Assert.Equal(pending.Groups.Sum(g => g.Count), pending.Total);
        var summary = await superAdmin.GetFromJsonAsync<PendingActionsSummaryResponse>("/api/v1/pending-actions/summary", TestJson.Options);
        Assert.Equal(pending.Total, summary!.Total);

        // Dealt with: the deletion is refused, so it no longer counts.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            (await db.DeletionRequests.FindAsync(deletionId))!.Status = DeletionRequestStatus.Rejected;
            await db.SaveChangesAsync();
        }

        var after = await superAdmin.GetFromJsonAsync<PendingActionsResponse>("/api/v1/pending-actions", TestJson.Options);
        Assert.DoesNotContain(after!.Groups.SelectMany(g => g.Items), i => i.Id == deletionId && i.Link == "/deletion-requests");
        Assert.Equal(pending.Total - 1, after.Total);
    }

    [Fact]
    public async Task Only_super_admins_see_pending_actions()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "pending-actions-staff@bckash.test", ["clients.manage"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/pending-actions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/pending-actions/summary")).StatusCode);
    }
}
