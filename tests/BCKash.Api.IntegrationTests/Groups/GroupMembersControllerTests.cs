using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Groups;

public class GroupMembersControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public GroupMembersControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> CreateGroupAsync(HttpClient client, string name)
    {
        var request = new CreateGroupRequest(null, name, null, null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/groups", request);
        var created = await response.Content.ReadFromJsonAsync<GroupResponse>(TestJson.Options);
        return created!.Id;
    }

    private static async Task<int> CreateClientAsync(HttpClient client, string label)
    {
        var request = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Add_and_remove_a_member_is_logged_who_and_when_and_history_is_retained()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "member-add-remove@bckash.test", ["groups.manage", "clients.manage"]);
        var groupId = await CreateGroupAsync(httpClient, "Roster Group");
        var clientId = await CreateClientAsync(httpClient, "Roster");

        var addResponse = await httpClient.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequest(clientId));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await addResponse.Content.ReadFromJsonAsync<GroupMemberResponse>(TestJson.Options);
        Assert.NotNull(added!.CreatedById);
        Assert.NotNull(added.CreatedAt);
        Assert.Null(added.RemovedAt);
        Assert.Equal(clientId, added.ClientId);

        var rosterResponse = await httpClient.GetFromJsonAsync<List<GroupMemberResponse>>($"/api/groups/{groupId}/members", TestJson.Options);
        Assert.Single(rosterResponse!);

        var removeResponse = await httpClient.DeleteAsync($"/api/groups/{groupId}/members/{added.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var rosterAfterRemoval = await httpClient.GetFromJsonAsync<List<GroupMemberResponse>>($"/api/groups/{groupId}/members", TestJson.Options);
        Assert.Empty(rosterAfterRemoval!);

        var fullHistory = await httpClient.GetFromJsonAsync<List<GroupMemberResponse>>($"/api/groups/{groupId}/members?includeRemoved=true", TestJson.Options);
        var historical = Assert.Single(fullHistory!);
        Assert.NotNull(historical.RemovedAt);
        Assert.NotNull(historical.RemovedById);
    }

    [Fact]
    public async Task Re_adding_a_removed_client_creates_a_new_history_entry_not_a_revived_one()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "member-readd@bckash.test", ["groups.manage", "clients.manage"]);
        var groupId = await CreateGroupAsync(httpClient, "Re-add Group");
        var clientId = await CreateClientAsync(httpClient, "Readd");

        var first = await (await httpClient.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequest(clientId))).Content.ReadFromJsonAsync<GroupMemberResponse>(TestJson.Options);
        await httpClient.DeleteAsync($"/api/groups/{groupId}/members/{first!.Id}");

        var secondResponse = await httpClient.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequest(clientId));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await secondResponse.Content.ReadFromJsonAsync<GroupMemberResponse>(TestJson.Options);
        Assert.NotEqual(first.Id, second!.Id);

        var fullHistory = await httpClient.GetFromJsonAsync<List<GroupMemberResponse>>($"/api/groups/{groupId}/members?includeRemoved=true", TestJson.Options);
        Assert.Equal(2, fullHistory!.Count);
    }

    [Fact]
    public async Task Adding_an_already_active_member_is_rejected()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "member-duplicate@bckash.test", ["groups.manage", "clients.manage"]);
        var groupId = await CreateGroupAsync(httpClient, "Duplicate Group");
        var clientId = await CreateClientAsync(httpClient, "Duplicate");

        await httpClient.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequest(clientId));
        var secondAdd = await httpClient.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequest(clientId));

        Assert.Equal(HttpStatusCode.Conflict, secondAdd.StatusCode);
    }

    [Fact]
    public async Task Members_for_a_nonexistent_group_return_404()
    {
        var httpClient = await AuthenticatedClientFactory.CreateAsync(_factory, "member-404@bckash.test", "groups.manage");

        var response = await httpClient.GetAsync("/api/groups/999999/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
