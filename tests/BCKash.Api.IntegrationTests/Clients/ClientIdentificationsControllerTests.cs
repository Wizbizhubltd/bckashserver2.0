using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientIdentificationsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientIdentificationsControllerTests(BCKashWebApplicationFactory factory)
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
    public async Task Create_list_update_and_delete_an_identification()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "identification-crud@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "Ident");

        var createResponse = await client.PostAsJsonAsync($"/api/v1/clients/{clientId}/identifications",
            new SaveClientIdentificationRequest(null, "A1234567", true, "Passport"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientIdentificationResponse>(TestJson.Options);

        var listResponse = await client.GetFromJsonAsync<List<ClientIdentificationResponse>>($"/api/v1/clients/{clientId}/identifications", TestJson.Options);
        Assert.Single(listResponse!);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}/identifications/{created!.Id}",
            new SaveClientIdentificationRequest(null, "A1234567", false, "Deactivated"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClientIdentificationResponse>(TestJson.Options);
        Assert.False(updated!.Active);

        var deleteResponse = await client.DeleteAsync($"/api/v1/clients/{clientId}/identifications/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Identifications_for_a_nonexistent_client_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "identification-404@bckash.test", "clients.manage");

        var response = await client.GetAsync("/api/v1/clients/999999/identifications");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Writing_without_the_manage_permission_is_forbidden()
    {
        var manager = await AuthenticatedClientFactory.CreateAsync(_factory, "identification-perm-setup@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(manager, "Perm");

        var readOnly = await AuthenticatedClientFactory.CreateAsync(_factory, "identification-perm@bckash.test", "some.other.permission");

        var response = await readOnly.PostAsJsonAsync($"/api/v1/clients/{clientId}/identifications", new SaveClientIdentificationRequest(null, "X", true, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
