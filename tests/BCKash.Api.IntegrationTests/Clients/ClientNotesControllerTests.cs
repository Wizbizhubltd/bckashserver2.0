using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientNotesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientNotesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
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
    public async Task Create_list_update_and_delete_a_note()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "note-crud@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "Note");

        var createResponse = await client.PostAsJsonAsync($"/api/clients/{clientId}/notes", new SaveNoteRequest("First contact made."));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<NoteResponse>(TestJson.Options);
        Assert.NotNull(created!.CreatedById);

        var listResponse = await client.GetFromJsonAsync<List<NoteResponse>>($"/api/clients/{clientId}/notes", TestJson.Options);
        Assert.Single(listResponse!);

        var updateResponse = await client.PutAsJsonAsync($"/api/clients/{clientId}/notes/{created.Id}", new SaveNoteRequest("Follow-up scheduled."));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NoteResponse>(TestJson.Options);
        Assert.Equal("Follow-up scheduled.", updated!.Notes);
        Assert.NotNull(updated.ModifiedById);

        var deleteResponse = await client.DeleteAsync($"/api/clients/{clientId}/notes/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Notes_for_a_nonexistent_client_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "note-404@bckash.test", "clients.manage");

        var response = await client.GetAsync("/api/clients/999999/notes");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
