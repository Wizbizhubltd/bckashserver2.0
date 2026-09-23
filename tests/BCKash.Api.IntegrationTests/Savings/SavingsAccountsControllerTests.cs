using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

public class SavingsAccountsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsAccountsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions = ["savings-products.manage", "savings-accounts.manage", "clients.manage"];

    private static SaveSavingsProductRequest ProductRequest(string name, decimal interestRate = 5m) => new(
        Name: name, ShortName: name, Description: null, CurrencyId: null, Decimals: 2,
        InterestRate: interestRate, AllowOverdraft: false, MinimumBalance: 1000m,
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
        var response = await client.PostAsJsonAsync("/api/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Open_then_approve_sets_balances_and_next_interest_dates()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-account-open@bckash.test", AllPermissions);

        var product = await (await client.PostAsJsonAsync("/api/savings-products", ProductRequest("Approve Test"))).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        var clientId = await CreateClientAsync(client, "SavOpen");

        var openResponse = await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, clientId, null, null, product!.Id, null));
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var account = await openResponse.Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        Assert.Equal(SavingsAccountStatus.Pending, account!.Status);
        Assert.StartsWith("SV", account.AccountNumber);

        var approveResponse = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/approve", new ApproveSavingsAccountRequest(5000m, null, new DateOnly(2026, 1, 1), "Approved"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);

        Assert.Equal(SavingsAccountStatus.Approved, approved!.Status);
        Assert.Equal(5000m, approved.Balance);
        Assert.Equal(5m, approved.InterestRate);
        Assert.Equal(new DateOnly(2026, 2, 1), approved.NextInterestCalculationDate);
        Assert.Equal(new DateOnly(2026, 2, 1), approved.NextInterestPostingDate);
    }

    [Fact]
    public async Task Declining_a_pending_account_requires_a_reason()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-account-decline@bckash.test", AllPermissions);

        var product = await (await client.PostAsJsonAsync("/api/savings-products", ProductRequest("Decline Test"))).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        var clientId = await CreateClientAsync(client, "SavDecline");
        var account = await (await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, clientId, null, null, product!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);

        var blankReasonResponse = await client.PostAsJsonAsync($"/api/savings-accounts/{account!.Id}/decline", new DeclineSavingsAccountRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, blankReasonResponse.StatusCode);

        var declineResponse = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/decline", new DeclineSavingsAccountRequest("Failed KYC"));
        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        var declined = await declineResponse.Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        Assert.Equal(SavingsAccountStatus.Declined, declined!.Status);
    }

    [Fact]
    public async Task Closing_an_account_with_a_non_zero_balance_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-account-close@bckash.test", AllPermissions);

        var product = await (await client.PostAsJsonAsync("/api/savings-products", ProductRequest("Close Test"))).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        var clientId = await CreateClientAsync(client, "SavClose");
        var account = await (await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, clientId, null, null, product!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        await client.PostAsJsonAsync($"/api/savings-accounts/{account!.Id}/approve", new ApproveSavingsAccountRequest(1000m, null, new DateOnly(2026, 1, 1), null));

        var closeResponse = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/close", "leftover balance");
        Assert.Equal(HttpStatusCode.Conflict, closeResponse.StatusCode);
    }
}
