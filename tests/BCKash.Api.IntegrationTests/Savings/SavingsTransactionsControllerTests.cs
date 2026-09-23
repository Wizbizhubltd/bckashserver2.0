using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

public class SavingsTransactionsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsTransactionsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions = ["savings-products.manage", "savings-accounts.manage", "clients.manage"];

    private static SaveSavingsProductRequest ProductRequest(string name, bool allowOverdraft = false, decimal? minimumBalance = 1000m) => new(
        Name: name, ShortName: name, Description: null, CurrencyId: null, Decimals: 2,
        InterestRate: 5m, AllowOverdraft: allowOverdraft, MinimumBalance: minimumBalance,
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

    private static async Task<SavingsAccountResponse> OpenAndApproveAsync(HttpClient client, string label, SaveSavingsProductRequest productRequest, decimal openingBalance, decimal? overdraftLimit = null)
    {
        var product = await (await client.PostAsJsonAsync("/api/savings-products", productRequest)).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);
        var clientId = await CreateClientAsync(client, label);
        var account = await (await client.PostAsJsonAsync("/api/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, clientId, null, null, product!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        var approved = await (await client.PostAsJsonAsync($"/api/savings-accounts/{account!.Id}/approve", new ApproveSavingsAccountRequest(openingBalance, overdraftLimit, new DateOnly(2026, 1, 1), null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        return approved!;
    }

    [Fact]
    public async Task Deposit_increases_balance()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-txn-deposit@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "TxnDeposit", ProductRequest("Deposit Product"), 1000m);

        var response = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/transactions/deposit", new RecordSavingsTransactionRequest(500m, new DateOnly(2026, 1, 15), "Top up"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var updated = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(1500m, updated!.Balance);
    }

    /// <summary>Acceptance criterion 1: withdrawal below minimum balance is rejected unless overdraft is enabled and within limit.</summary>
    [Fact]
    public async Task Withdrawal_below_minimum_balance_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-txn-withdraw-floor@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "TxnFloor", ProductRequest("Floor Product", minimumBalance: 1000m), 1500m);

        // 1500 - 600 = 900, below the 1000 minimum.
        var response = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/transactions/withdrawal", new RecordSavingsTransactionRequest(600m, new DateOnly(2026, 1, 20), null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var unchanged = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(1500m, unchanged!.Balance);
    }

    [Fact]
    public async Task Withdrawal_within_overdraft_limit_succeeds()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-txn-withdraw-overdraft@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "TxnOverdraft", ProductRequest("Overdraft Product", allowOverdraft: true, minimumBalance: 1000m), 200m, overdraftLimit: 500m);

        // 200 - 600 = -400, within the -500 overdraft floor.
        var response = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/transactions/withdrawal", new RecordSavingsTransactionRequest(600m, new DateOnly(2026, 1, 20), null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var updated = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(-400m, updated!.Balance);
    }

    [Fact]
    public async Task Reversing_a_deposit_restores_the_prior_balance()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-txn-reverse@bckash.test", AllPermissions);
        var account = await OpenAndApproveAsync(client, "TxnReverse", ProductRequest("Reverse Product"), 1000m);

        var depositResponse = await client.PostAsJsonAsync($"/api/savings-accounts/{account.Id}/transactions/deposit", new RecordSavingsTransactionRequest(300m, new DateOnly(2026, 1, 15), null));
        var deposit = await depositResponse.Content.ReadFromJsonAsync<SavingsTransactionResponse>(TestJson.Options);

        var afterDeposit = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(1300m, afterDeposit!.Balance);

        var reverseResponse = await client.PostAsync($"/api/savings-accounts/{account.Id}/transactions/{deposit!.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);

        var afterReversal = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/savings-accounts/{account.Id}", TestJson.Options);
        Assert.Equal(1000m, afterReversal!.Balance);
    }
}
