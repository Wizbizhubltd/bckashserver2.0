using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

public class SavingsProductsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsProductsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static SaveSavingsProductRequest ProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, CurrencyId: null, Decimals: 2,
        InterestRate: 5m, AllowOverdraft: false, MinimumBalance: 1000m,
        InterestCompoundingPeriod: InterestCompoundingPeriod.Monthly,
        InterestPostingPeriod: InterestPostingPeriod.Monthly,
        InterestCalculationType: InterestCalculationType.Daily,
        AllowTransferWithdrawalFee: false, OpeningBalance: 0m, AllowAdditionalCharges: true,
        YearDays: SavingsYearDays.Days365, AccountingRule: SavingsAccountingRule.Cash,
        GlAccountSavingsReferenceId: null, GlAccountOverdraftPortfolioId: null, GlAccountSavingsControlId: null,
        GlAccountInterestOnSavingsId: null, GlAccountSavingsWrittenOffId: null, GlAccountIncomeInterestId: null,
        GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null);

    [Fact]
    public async Task Create_and_fetch_a_savings_product()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-product-crud@bckash.test", "savings-products.manage");

        var response = await client.PostAsJsonAsync("/api/savings-products", ProductRequest("Regular Savings"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        Assert.Equal("Regular Savings", created!.Name);
        Assert.True(created.Active);
    }

    [Fact]
    public async Task Deactivate_and_reactivate_a_product()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-product-deactivate@bckash.test", "savings-products.manage");

        var created = await (await client.PostAsJsonAsync("/api/savings-products", ProductRequest("Deactivate Me")))
            .Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);

        var deactivated = await (await client.PostAsync($"/api/savings-products/{created!.Id}/deactivate", null)).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        Assert.False(deactivated!.Active);

        var reactivated = await (await client.PostAsync($"/api/savings-products/{created.Id}/activate", null)).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        Assert.True(reactivated!.Active);
    }

    [Fact]
    public async Task Deleting_a_product_with_an_account_against_it_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-product-inuse@bckash.test", ["savings-products.manage", "savings-accounts.manage", "clients.manage"]);

        var product = await (await client.PostAsJsonAsync("/api/savings-products", ProductRequest("In Use Product"))).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);

        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, "SavProd", null, "Borrower", "SavProd Borrower",
            null, "SavProd Borrower", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, borrower!.Id, null, null, product!.Id, null));

        var deleteResponse = await client.DeleteAsync($"/api/savings-products/{product.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }
}
