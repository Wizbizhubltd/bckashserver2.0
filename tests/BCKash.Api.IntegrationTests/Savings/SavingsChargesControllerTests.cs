using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

public class SavingsChargesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsChargesControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions = ["savings-products.manage", "savings-accounts.manage", "clients.manage"];

    private static SaveSavingsProductRequest ProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, CurrencyId: null, Decimals: 2,
        InterestRate: 5m, AllowOverdraft: false, MinimumBalance: 100m,
        InterestCompoundingPeriod: InterestCompoundingPeriod.Monthly, InterestPostingPeriod: InterestPostingPeriod.Monthly,
        InterestCalculationType: InterestCalculationType.Daily,
        AllowTransferWithdrawalFee: false, OpeningBalance: 0m, AllowAdditionalCharges: true,
        YearDays: SavingsYearDays.Days365, AccountingRule: SavingsAccountingRule.Cash,
        GlAccountSavingsReferenceId: null, GlAccountOverdraftPortfolioId: null, GlAccountSavingsControlId: null,
        GlAccountInterestOnSavingsId: null, GlAccountSavingsWrittenOffId: null, GlAccountIncomeInterestId: null,
        GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null);

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

    private static async Task<SavingsAccountResponse> OpenAndApproveAsync(HttpClient client, string label, decimal openingBalance)
    {
        var product = await (await client.PostAsJsonAsync("/api/v1/savings-products", ProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        var clientId = await CreateClientAsync(client, label);
        var account = await (await client.PostAsJsonAsync("/api/v1/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, clientId, null, null, product!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        var approved = await (await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account!.Id}/approve", new ApproveSavingsAccountRequest(openingBalance, null, new DateOnly(2026, 1, 1), null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        return approved!;
    }

    [Fact]
    public async Task Attach_pay_reduces_balance_and_marks_paid()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-charge-pay@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "ChargePay", 1000m);

        var attachResponse = await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account.Id}/charges", new AttachSavingsChargeRequest(SavingsChargeType.AnnualFee, false, 50m, new DateOnly(2026, 2, 1)));
        Assert.Equal(HttpStatusCode.Created, attachResponse.StatusCode);
        var charge = await attachResponse.Content.ReadFromJsonAsync<SavingsChargeResponse>(TestJson.Options);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account.Id}/charges/{charge!.Id}/pay", new DateOnly(2026, 2, 1));
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);
        var paid = await payResponse.Content.ReadFromJsonAsync<SavingsChargeResponse>(TestJson.Options);
        Assert.Equal(50m, paid!.AmountPaid);

        var updated = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/v1/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(950m, updated!.Balance);
    }

    [Fact]
    public async Task Waiving_a_charge_blocks_payment()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-charge-waive@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "ChargeWaive", 1000m);

        var charge = await (await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account.Id}/charges", new AttachSavingsChargeRequest(SavingsChargeType.MonthlyFee, false, 20m, null)))
            .Content.ReadFromJsonAsync<SavingsChargeResponse>(TestJson.Options);

        var waiveResponse = await client.PostAsync($"/api/v1/savings-accounts/{account.Id}/charges/{charge!.Id}/waive", null);
        Assert.Equal(HttpStatusCode.OK, waiveResponse.StatusCode);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account.Id}/charges/{charge.Id}/pay", (DateOnly?)null);
        Assert.Equal(HttpStatusCode.Conflict, payResponse.StatusCode);
    }
}
