using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Groups;

/// <summary>The clients page's bulk actions: reassigning groups to a marketer, disabling/enabling groups, and moving clients between groups.</summary>
public class GroupBulkActionsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GroupBulkActionsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<HttpClient> ClientAsync(string email) => AuthenticatedClientFactory.CreateAsync(_factory, email, ["groups.manage", "clients.manage"]);

    private static Group NewGroup(int officeId, GroupStatus status = GroupStatus.Active, int? staffId = null) =>
        new() { Name = $"Group {Guid.NewGuid():N}", OfficeId = officeId, Status = status, StaffId = staffId, CreatedAt = DateTime.UtcNow };

    private static Client NewClient(int officeId, string name) =>
        new() { FirstName = name, LastName = "Client", OfficeId = officeId, Status = ClientStatus.Active, CreatedAt = DateTime.UtcNow };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    [Fact]
    public async Task Reassigning_groups_moves_the_group_and_its_members_to_a_marketer_in_the_same_office()
    {
        var http = await ClientAsync("group-bulk-reassign@bckash.test");
        int sameOfficeGroupId, otherOfficeGroupId, marketerId, memberId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var otherOffice = await TestDataSeeder.SeedOfficeAsync(db);
            marketerId = (await TestDataSeeder.SeedTypedUserAsync(db, "group-bulk-marketer@bckash.test", "Correct-Password1!", UserTypeSlugs.Marketer, office.Id)).Id;

            var sameOffice = NewGroup(office.Id);
            var elsewhere = NewGroup(otherOffice.Id);
            var member = NewClient(office.Id, "Member");
            db.AddRange(sameOffice, elsewhere, member);
            await db.SaveChangesAsync();
            db.GroupClients.Add(new GroupClient { GroupId = sameOffice.Id, ClientId = member.Id, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            (sameOfficeGroupId, otherOfficeGroupId, memberId) = (sameOffice.Id, elsewhere.Id, member.Id);
        }

        var result = await ReadAsync<BulkActionResponse>(await http.PostAsJsonAsync(
            "/api/v1/groups/bulk/reassign-marketer", new BulkReassignMarketerRequest([sameOfficeGroupId, otherOfficeGroupId], marketerId), TestJson.Options));

        Assert.Equal(1, result.Succeeded);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal(otherOfficeGroupId, skip.Id);
        Assert.Equal("The marketer works in a different office.", skip.Reason);

        using var verify = _factory.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<BCKashDbContext>();
        Assert.Equal(marketerId, (await check.Groups.SingleAsync(g => g.Id == sameOfficeGroupId)).StaffId);
        Assert.Equal(marketerId, (await check.Clients.SingleAsync(c => c.Id == memberId)).StaffId);
        Assert.Null((await check.Groups.SingleAsync(g => g.Id == otherOfficeGroupId)).StaffId);
    }

    [Fact]
    public async Task Groups_can_be_disabled_and_enabled_in_bulk()
    {
        var http = await ClientAsync("group-bulk-toggle@bckash.test");
        int activeId, pendingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var active = NewGroup(office.Id);
            var pending = NewGroup(office.Id, GroupStatus.Pending);
            db.AddRange(active, pending);
            await db.SaveChangesAsync();
            (activeId, pendingId) = (active.Id, pending.Id);
        }

        var disabled = await ReadAsync<BulkActionResponse>(await http.PostAsJsonAsync(
            "/api/v1/groups/bulk/deactivate", new BulkDeactivateGroupsRequest([activeId, pendingId], "Merged into another group"), TestJson.Options));
        Assert.Equal(1, disabled.Succeeded);
        Assert.Equal(pendingId, Assert.Single(disabled.Skipped).Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var group = await scope.ServiceProvider.GetRequiredService<BCKashDbContext>().Groups.SingleAsync(g => g.Id == activeId);
            Assert.Equal(GroupStatus.Inactive, group.Status);
            Assert.Equal("Merged into another group", group.InactiveReason);
        }

        var enabled = await ReadAsync<BulkActionResponse>(await http.PostAsJsonAsync("/api/v1/groups/bulk/reactivate", new BulkGroupsRequest([activeId]), TestJson.Options));
        Assert.Equal(1, enabled.Succeeded);

        using var verify = _factory.Services.CreateScope();
        Assert.Equal(GroupStatus.Active, (await verify.ServiceProvider.GetRequiredService<BCKashDbContext>().Groups.SingleAsync(g => g.Id == activeId)).Status);
    }

    [Fact]
    public async Task Disabling_groups_needs_a_reason()
    {
        var http = await ClientAsync("group-bulk-no-reason@bckash.test");

        var response = await http.PostAsJsonAsync("/api/v1/groups/bulk/deactivate", new BulkDeactivateGroupsRequest([1], " "), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Moving_clients_keeps_back_those_with_active_loans_and_defaulters()
    {
        var http = await ClientAsync("group-bulk-move@bckash.test");
        int targetId, oldGroupId, freeId, activeLoanId, defaulterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var target = NewGroup(office.Id);
            var oldGroup = NewGroup(office.Id);
            var free = NewClient(office.Id, "Free");
            var withLoan = NewClient(office.Id, "Borrower");
            var defaulter = NewClient(office.Id, "Defaulter");
            db.AddRange(target, oldGroup, free, withLoan, defaulter);
            await db.SaveChangesAsync();

            db.GroupClients.AddRange(
                new GroupClient { GroupId = oldGroup.Id, ClientId = free.Id, CreatedAt = DateTime.UtcNow },
                new GroupClient { GroupId = oldGroup.Id, ClientId = withLoan.Id, CreatedAt = DateTime.UtcNow });
            db.Loans.AddRange(
                new Loan { ClientId = free.Id, Status = LoanStatus.Closed, AccountNumber = "MOVE-CLOSED" },
                new Loan { ClientId = withLoan.Id, Status = LoanStatus.Disbursed, AccountNumber = "MOVE-RUNNING" },
                new Loan { ClientId = defaulter.Id, Status = LoanStatus.WrittenOff, AccountNumber = "MOVE-WRITTEN-OFF" });
            await db.SaveChangesAsync();
            (targetId, oldGroupId, freeId, activeLoanId, defaulterId) = (target.Id, oldGroup.Id, free.Id, withLoan.Id, defaulter.Id);
        }

        var result = await ReadAsync<BulkActionResponse>(await http.PostAsJsonAsync(
            $"/api/v1/groups/{targetId}/members/bulk-move", new BulkMoveClientsRequest([freeId, activeLoanId, defaulterId]), TestJson.Options));

        Assert.Equal(1, result.Succeeded);
        Assert.Equal("Has an active loan.", result.Skipped.Single(s => s.Id == activeLoanId).Reason);
        Assert.Equal("Is a defaulter.", result.Skipped.Single(s => s.Id == defaulterId).Reason);

        using var verify = _factory.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var freeMemberships = await check.GroupClients.Where(gc => gc.ClientId == freeId && gc.RemovedAt == null).ToListAsync();
        Assert.Equal(targetId, Assert.Single(freeMemberships).GroupId);
        Assert.True(await check.GroupClients.AnyAsync(gc => gc.ClientId == activeLoanId && gc.GroupId == oldGroupId && gc.RemovedAt == null));

        var clients = await http.GetFromJsonAsync<PagedResult<ClientListItemResponse>>("/api/v1/clients?search=Free&pageSize=100", TestJson.Options);
        Assert.Equal(targetId, clients!.Items.Single(c => c.Id == freeId).GroupId);
    }

    [Fact]
    public async Task Clients_cannot_be_moved_into_a_disabled_group()
    {
        var http = await ClientAsync("group-bulk-move-closed@bckash.test");
        int targetId, clientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var office = await TestDataSeeder.SeedOfficeAsync(db);
            var target = NewGroup(office.Id, GroupStatus.Inactive);
            var client = NewClient(office.Id, "Closed");
            db.AddRange(target, client);
            await db.SaveChangesAsync();
            (targetId, clientId) = (target.Id, client.Id);
        }

        var result = await ReadAsync<BulkActionResponse>(await http.PostAsJsonAsync(
            $"/api/v1/groups/{targetId}/members/bulk-move", new BulkMoveClientsRequest([clientId]), TestJson.Options));

        Assert.Equal(0, result.Succeeded);
        Assert.Equal("The group isn't taking members.", Assert.Single(result.Skipped).Reason);
    }
}
