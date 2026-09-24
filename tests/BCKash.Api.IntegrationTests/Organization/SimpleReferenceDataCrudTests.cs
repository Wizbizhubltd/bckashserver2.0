using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using Xunit;

namespace BCKash.Api.IntegrationTests.Organization;

/// <summary>CRUD round-trip for the reference-data entities with no extra business rules attached.</summary>
public class SimpleReferenceDataCrudTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SimpleReferenceDataCrudTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Currency_can_be_created_updated_and_deleted()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "crud-currency@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/currencies", new SaveCurrencyRequest("US Dollar", "USD", "$", "2", 1m, "840", true));
        var created = await createResponse.Content.ReadFromJsonAsync<CurrencyResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/currencies/{created!.Id}", new SaveCurrencyRequest("US Dollar", "USD", "$", "2", 1600m, "840", true));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CurrencyResponse>();
        Assert.Equal(1600m, updated!.Xrate);

        var deleteResponse = await client.DeleteAsync($"/api/v1/currencies/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/currencies/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Country_can_be_created_and_listed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "crud-country@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/countries", new SaveCountryRequest("XX", "Testland"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/v1/countries");
        var countries = await listResponse.Content.ReadFromJsonAsync<List<CountryResponse>>();
        Assert.Contains(countries!, c => c.Sortname == "XX");
    }

    [Fact]
    public async Task Fund_can_be_created_and_deleted()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "crud-fund@bckash.test", "organization.manage");

        var createResponse = await client.PostAsJsonAsync("/api/v1/funds", new SaveFundRequest("Grant Fund"));
        var created = await createResponse.Content.ReadFromJsonAsync<FundResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/v1/funds/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Payment_type_and_its_nested_payment_details_can_be_managed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "crud-payment-type@bckash.test", "organization.manage");

        var typeResponse = await client.PostAsJsonAsync("/api/v1/payment-types", new SavePaymentTypeRequest("Bank Transfer", null, false));
        var paymentType = await typeResponse.Content.ReadFromJsonAsync<PaymentTypeResponse>();

        var detailResponse = await client.PostAsJsonAsync($"/api/v1/payment-types/{paymentType!.Id}/details",
            new SavePaymentDetailRequest(paymentType.Id, "0123456789", null, "058", null, "GTBank", null));
        Assert.Equal(HttpStatusCode.Created, detailResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/v1/payment-types/{paymentType.Id}/details");
        var details = await listResponse.Content.ReadFromJsonAsync<List<PaymentDetailResponse>>();
        Assert.Single(details!);
        Assert.Equal("GTBank", details![0].Bank);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected_across_the_module()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/offices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
