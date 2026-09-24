using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientNextOfGuardiansControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientNextOfGuardiansControllerTests(BCKashWebApplicationFactory factory)
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
    public async Task Create_list_update_and_delete_a_next_of_guardian_record()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "guardian-crud@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "Guardian");

        var createResponse = await client.PostAsJsonAsync($"/api/v1/clients/{clientId}/next-of-guardians",
            new SaveClientNextOfGuardianRequest(null, null, "Emeka", null, "Nwosu", null, null, null, null, null, "08077776666", null, null, null, "Uncle"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientNextOfGuardianResponse>(TestJson.Options);

        var listResponse = await client.GetFromJsonAsync<List<ClientNextOfGuardianResponse>>($"/api/v1/clients/{clientId}/next-of-guardians", TestJson.Options);
        Assert.Single(listResponse!);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}/next-of-guardians/{created!.Id}",
            new SaveClientNextOfGuardianRequest(null, null, "Emeka", null, "Nwosu-Updated", null, null, null, null, null, "08077776666", null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClientNextOfGuardianResponse>(TestJson.Options);
        Assert.Equal("Nwosu-Updated", updated!.LastName);

        var deleteResponse = await client.DeleteAsync($"/api/v1/clients/{clientId}/next-of-guardians/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Next_of_guardians_for_a_nonexistent_client_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "guardian-404@bckash.test", "clients.manage");

        var response = await client.GetAsync("/api/v1/clients/999999/next-of-guardians");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
