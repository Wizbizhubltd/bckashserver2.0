using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientNextOfKinControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientNextOfKinControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> CreateClientAsync(HttpClient client, string label)
    {
        var request = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Create_list_update_and_delete_a_next_of_kin_record()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "kin-crud@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "Kin");
        var sister = await TestDataSeeder.SeedRelationshipAsync(_factory);

        var createResponse = await client.PostAsJsonAsync($"/api/v1/clients/{clientId}/next-of-kin",
            new SaveClientNextOfKinRequest(sister, null, "Chidi", null, "Okoro", null, null, null, null, null, "08099998888", null, null, null, "Sister"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientNextOfKinResponse>(TestJson.Options);

        var listResponse = await client.GetFromJsonAsync<List<ClientNextOfKinResponse>>($"/api/v1/clients/{clientId}/next-of-kin", TestJson.Options);
        Assert.Single(listResponse!);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}/next-of-kin/{created!.Id}",
            new SaveClientNextOfKinRequest(sister, null, "Chidi", null, "Okoro-Updated", null, null, null, null, null, "08099998888", null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClientNextOfKinResponse>(TestJson.Options);
        Assert.Equal("Okoro-Updated", updated!.LastName);

        var deleteResponse = await client.DeleteAsync($"/api/v1/clients/{clientId}/next-of-kin/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task A_next_of_kin_needs_a_real_relationship_and_a_name_and_is_told_why_otherwise()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "kin-validate@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "KinValidate");
        var friend = await TestDataSeeder.SeedRelationshipAsync(_factory, "Friend");

        async Task<(HttpStatusCode Status, string Body)> SaveAsync(int? relationshipId, string? firstName, string? mobile)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/clients/{clientId}/next-of-kin",
                new SaveClientNextOfKinRequest(relationshipId, null, firstName, null, "Okoro", null, null, null, null, null, mobile, null, null, null, null));
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        // An id that isn't a relationship type used to fail with a database error (500).
        var unknown = await SaveAsync(999_999, "Chidi", null);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.Status);
        Assert.Contains("choose one from the list", unknown.Body);

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(null, "Chidi", null)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(friend, "  ", null)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(friend, "Chidi", "0803")).Status);

        var saved = await SaveAsync(friend, "Chidi", "08031234567");
        Assert.Equal(HttpStatusCode.Created, saved.Status);
        Assert.Contains("+2348031234567", saved.Body);
    }

    [Fact]
    public async Task Next_of_kin_for_a_nonexistent_client_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "kin-404@bckash.test", "clients.manage");

        var response = await client.GetAsync("/api/v1/clients/999999/next-of-kin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
