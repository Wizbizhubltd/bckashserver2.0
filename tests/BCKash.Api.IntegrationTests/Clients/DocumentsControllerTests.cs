using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCKash.Api.IntegrationTests.Clients;

public class DocumentsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    // Minimal-but-recognizable byte sequences — the storage layer is byte-content-agnostic,
    // so these don't need to be fully valid renderable files, just carry the right extension/magic bytes.
    private static readonly byte[] PdfBytes = "%PDF-1.4\n%%EOF"u8.ToArray();
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private readonly BCKashWebApplicationFactory _factory;

    public DocumentsControllerTests(BCKashWebApplicationFactory factory)
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

    [Theory]
    [InlineData("statement.pdf", "application/pdf")]
    [InlineData("photo.png", "image/png")]
    public async Task Upload_and_download_round_trips_the_exact_bytes(string fileName, string expectedContentType)
    {
        var bytes = fileName.EndsWith(".pdf", StringComparison.Ordinal) ? PdfBytes : PngBytes;
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"document-{fileName}@bckash.test", "clients.manage");
        var clientId = await CreateClientAsync(client, "Doc");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent("KYC document"), "notes");

        var uploadResponse = await client.PostAsync($"/api/clients/{clientId}/documents", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>(TestJson.Options);
        Assert.Equal(fileName, uploaded!.Name);
        Assert.Equal(bytes.Length.ToString(), uploaded.Size);
        Assert.Equal(ReferenceEntityType.Client, uploaded.Type);
        Assert.Equal(clientId, uploaded.RecordId);

        var listResponse = await client.GetFromJsonAsync<List<DocumentResponse>>($"/api/clients/{clientId}/documents", TestJson.Options);
        Assert.Single(listResponse!);

        var downloadResponse = await client.GetAsync($"/api/clients/{clientId}/documents/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(expectedContentType, downloadResponse.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloadedBytes);

        string location;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            location = (await db.Documents.FindAsync(uploaded.Id))!.Location!;
        }

        var storageSettings = _factory.Services.GetRequiredService<IOptions<FileStorageSettings>>().Value;
        var absolutePath = Path.Combine(AppContext.BaseDirectory, storageSettings.RootPath, location.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolutePath));

        var deleteResponse = await client.DeleteAsync($"/api/clients/{clientId}/documents/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.False(File.Exists(absolutePath));

        var afterDeleteResponse = await client.GetAsync($"/api/clients/{clientId}/documents/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, afterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Documents_for_a_nonexistent_client_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "document-404@bckash.test", "clients.manage");

        var response = await client.GetAsync("/api/clients/999999/documents");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
