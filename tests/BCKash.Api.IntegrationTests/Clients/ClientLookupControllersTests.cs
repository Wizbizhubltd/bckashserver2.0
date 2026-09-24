using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class ClientRelationshipsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientRelationshipsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_list_update_and_delete_a_relationship()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "relationship-crud@bckash.test", "clients.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/client-relationships", new SaveClientRelationshipRequest("Sibling"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientRelationshipResponse>(TestJson.Options);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/client-relationships/{created!.Id}", new SaveClientRelationshipRequest("Sibling (Updated)"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/v1/client-relationships/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}

public class ClientIdentificationTypesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientIdentificationTypesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_list_update_and_delete_an_identification_type()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "id-type-crud@bckash.test", "clients.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/client-identification-types", new SaveClientIdentificationTypeRequest("National ID"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientIdentificationTypeResponse>(TestJson.Options);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/client-identification-types/{created!.Id}", new SaveClientIdentificationTypeRequest("National ID (Updated)"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/v1/client-identification-types/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}

public class ClientProfessionsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public ClientProfessionsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_list_update_and_delete_a_profession()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "profession-crud@bckash.test", "clients.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/client-professions", new SaveClientProfessionRequest("Trader"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientProfessionResponse>(TestJson.Options);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/client-professions/{created!.Id}", new SaveClientProfessionRequest("Trader (Updated)"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/v1/client-professions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
