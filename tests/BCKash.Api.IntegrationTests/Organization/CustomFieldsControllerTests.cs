using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

public class CustomFieldsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public CustomFieldsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static IEnumerable<object[]> EveryFieldTypeWithAValidValue()
    {
        yield return [CustomFieldType.Number, null!, "42"];
        yield return [CustomFieldType.Textfield, null!, "free text"];
        yield return [CustomFieldType.Date, null!, "2026-01-15"];
        yield return [CustomFieldType.Decimal, null!, "42.50"];
        yield return [CustomFieldType.Textarea, null!, "a longer block of free text"];
        yield return [CustomFieldType.Checkbox, "Red,Green,Blue", "Red,Blue"];
        yield return [CustomFieldType.Radiobox, "Yes,No", "Yes"];
        yield return [CustomFieldType.Select, "Small,Medium,Large", "Medium"];
    }

    [Theory]
    [MemberData(nameof(EveryFieldTypeWithAValidValue))]
    public async Task Every_field_type_can_be_defined_and_a_value_captured_against_a_test_record(
        CustomFieldType fieldType, string? options, string value)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"customfield-{fieldType}@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldRequest(
            "client", $"Test {fieldType}", fieldType, false, options, options, options));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var field = await createResponse.Content.ReadFromJsonAsync<CustomFieldResponse>(TestJson.Options);

        var captureResponse = await client.PostAsJsonAsync("/api/custom-fields/values", new CaptureCustomFieldValueRequest(
            field!.Id, "Client", 12345, value));

        Assert.True(
            captureResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected success capturing a {fieldType} value, got {captureResponse.StatusCode}: {await captureResponse.Content.ReadAsStringAsync()}");

        var captured = await captureResponse.Content.ReadFromJsonAsync<CustomFieldValueResponse>();
        Assert.Equal(value, captured!.Value);
        Assert.Equal("Client", captured.EntityType);
        Assert.Equal(12345, captured.EntityId);
    }

    [Fact]
    public async Task Capturing_a_value_outside_the_configured_select_options_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "customfield-invalid-select@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldRequest(
            "client", "Shirt Size", CustomFieldType.Select, false, null, null, "Small,Medium,Large"));
        var field = await createResponse.Content.ReadFromJsonAsync<CustomFieldResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync("/api/custom-fields/values", new CaptureCustomFieldValueRequest(
            field!.Id, "Client", 99, "ExtraLarge"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Capturing_a_value_twice_for_the_same_record_updates_it_in_place()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "customfield-upsert@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldRequest(
            "client", "Notes", CustomFieldType.Textfield, false, null, null, null));
        var field = await createResponse.Content.ReadFromJsonAsync<CustomFieldResponse>(TestJson.Options);

        await client.PostAsJsonAsync("/api/custom-fields/values", new CaptureCustomFieldValueRequest(field!.Id, "Client", 7, "first value"));
        await client.PostAsJsonAsync("/api/custom-fields/values", new CaptureCustomFieldValueRequest(field.Id, "Client", 7, "second value"));

        var listResponse = await client.GetAsync($"/api/custom-fields/values?entityType=Client&entityId=7");
        var values = await listResponse.Content.ReadFromJsonAsync<List<CustomFieldValueResponse>>();

        Assert.Single(values!);
        Assert.Equal("second value", values![0].Value);
    }
}
