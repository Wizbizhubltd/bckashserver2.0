using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Groups;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Groups;

public class GroupsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GroupsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateGroupRequest NewGroupRequest(string name) =>
        new(OfficeId: null, Name: name, ExternalId: null, StaffId: null, JoinedDate: null,
            Mobile: null, Phone: null, Email: null,
            Street: null, Ward: null, District: null, Region: null, Address: null, Notes: null);

    [Fact]
    public async Task Create_and_fetch_a_group()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "group-crud@bckash.test", "groups.manage");

        var response = await client.PostAsJsonAsync("/api/v1/groups", NewGroupRequest("Market Women's Group"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<GroupResponse>(TestJson.Options);
        Assert.Equal("Market Women's Group", created!.Name);
        Assert.Equal(GroupStatus.Pending, created.Status);

        var getResponse = await client.GetAsync($"/api/v1/groups/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Update_applies_editable_fields_and_leaves_status_untouched()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "group-update@bckash.test", "groups.manage");
        var created = await (await client.PostAsJsonAsync("/api/v1/groups", NewGroupRequest("Original Name"))).Content.ReadFromJsonAsync<GroupResponse>(TestJson.Options);

        var updateRequest = new UpdateGroupRequest(
            OfficeId: null, Name: "Updated Name", ExternalId: null, StaffId: null, JoinedDate: null,
            Mobile: "08012345678", Phone: null, Email: null,
            Street: null, Ward: null, District: null, Region: null, Address: null, Notes: "Weekly meeting");

        var response = await client.PutAsJsonAsync($"/api/v1/groups/{created!.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<GroupResponse>(TestJson.Options);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("08012345678", updated.Mobile);
        Assert.Equal(GroupStatus.Pending, updated.Status);
    }

    public static IEnumerable<object[]> StatusTransitionCases()
    {
        GroupStatus[] statuses = [GroupStatus.Pending, GroupStatus.Active, GroupStatus.Inactive, GroupStatus.Declined, GroupStatus.Closed];
        string[] transitions = ["activate", "deactivate", "reactivate", "decline", "close"];

        foreach (var status in statuses)
        {
            foreach (var transition in transitions)
            {
                yield return [status, transition];
            }
        }
    }

    [Theory]
    [MemberData(nameof(StatusTransitionCases))]
    public async Task Status_transition_only_succeeds_from_its_valid_predecessor(GroupStatus fromStatus, string transition)
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, $"group-transition-{fromStatus}-{transition}@bckash.test", "groups.manage");

        int groupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            var entity = new Group { Status = fromStatus, Name = $"Seed-{Guid.NewGuid():N}" };
            db.Groups.Add(entity);
            await db.SaveChangesAsync();
            groupId = entity.Id;
        }

        var expectedAllowed = transition switch
        {
            "activate" => GroupStatusTransitionRules.CanActivate(fromStatus),
            "deactivate" => GroupStatusTransitionRules.CanDeactivate(fromStatus),
            "reactivate" => GroupStatusTransitionRules.CanReactivate(fromStatus),
            "decline" => GroupStatusTransitionRules.CanDecline(fromStatus),
            "close" => GroupStatusTransitionRules.CanClose(fromStatus),
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };

        var response = transition switch
        {
            "activate" => await httpClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/activate", new ActivateGroupRequest(null)),
            "deactivate" => await httpClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/deactivate", new ReasonRequest("Integration test reason")),
            "reactivate" => await httpClient.PostAsync($"/api/v1/groups/{groupId}/reactivate", null),
            "decline" => await httpClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/decline", new ReasonRequest("Integration test reason")),
            "close" => await httpClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/close", new ReasonRequest("Integration test reason")),
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };

        if (!expectedAllowed)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var updated = await assertDb.Groups.FirstAsync(g => g.Id == groupId);

        switch (transition)
        {
            case "activate":
                Assert.Equal(GroupStatus.Active, updated.Status);
                Assert.NotNull(updated.ActivatedDate);
                Assert.NotNull(updated.ActivatedById);
                break;
            case "deactivate":
                Assert.Equal(GroupStatus.Inactive, updated.Status);
                Assert.NotNull(updated.InactiveDate);
                Assert.NotNull(updated.InactiveById);
                Assert.Equal("Integration test reason", updated.InactiveReason);
                break;
            case "reactivate":
                Assert.Equal(GroupStatus.Active, updated.Status);
                Assert.NotNull(updated.ReactivatedDate);
                Assert.NotNull(updated.ReactivatedById);
                break;
            case "decline":
                Assert.Equal(GroupStatus.Declined, updated.Status);
                Assert.NotNull(updated.DeclinedDate);
                Assert.NotNull(updated.DeclinedById);
                Assert.Equal("Integration test reason", updated.DeclinedReason);
                break;
            case "close":
                Assert.Equal(GroupStatus.Closed, updated.Status);
                Assert.NotNull(updated.ClosedDate);
                Assert.NotNull(updated.ClosedById);
                Assert.Equal("Integration test reason", updated.ClosedReason);
                break;
        }

        var audited = await assertDb.AuditTrail
            .Where(a => a.Module == "Group" && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(audited);
    }

    [Fact]
    public async Task Search_and_filter_narrow_the_group_list()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "group-search@bckash.test", "groups.manage");

        var unique = Guid.NewGuid().ToString("N")[..8];
        await httpClient.PostAsJsonAsync("/api/v1/groups", NewGroupRequest($"Unity{unique} Group"));
        await httpClient.PostAsJsonAsync("/api/v1/groups", NewGroupRequest("Some Other Group"));

        var byName = await httpClient.GetFromJsonAsync<PagedResult<GroupListItemResponse>>($"/api/v1/groups?search=Unity{unique}", TestJson.Options);
        Assert.Equal(1, byName!.TotalCount);
        Assert.Equal($"Unity{unique} Group", byName.Items[0].Name);

        var noMatch = await httpClient.GetFromJsonAsync<PagedResult<GroupListItemResponse>>($"/api/v1/groups?search=NoSuchGroup{unique}", TestJson.Options);
        Assert.Equal(0, noMatch!.TotalCount);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/groups");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reads_are_allowed_without_the_manage_permission_but_writes_are_not()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
        var user = await TestDataSeeder.SeedUserAsync(db, "group-readonly@bckash.test", "Correct-Password1!");

        var loginClient = _factory.CreateClient();
        var tokens = await LoginTestHelper.LoginAndVerifyOtpAsync(_factory, loginClient, user.Email, "Correct-Password1!");

        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var readResponse = await httpClient.GetAsync("/api/v1/groups");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        var writeResponse = await httpClient.PostAsJsonAsync("/api/v1/groups", NewGroupRequest("No Permission"));
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }
}
