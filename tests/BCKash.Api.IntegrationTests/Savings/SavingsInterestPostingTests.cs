using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Savings;
using Xunit;

namespace BCKash.Api.IntegrationTests.Savings;

/// <summary>
/// Acceptance criteria 2 and 3: the interest accrual/posting engine produces correct interest
/// for both calculation types, hand-verifiable, and posting generates a balanced, correctly
/// traceable GL entry on the configured posting period. Uses explicit `asOfDate` parameters
/// throughout — never DateTime.UtcNow — so the expected figures don't depend on when the test
/// suite actually runs (same determinism concern as Phase 5's NPA tests).
/// </summary>
public class SavingsInterestPostingTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public SavingsInterestPostingTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions = ["savings-products.manage", "savings-accounts.manage", "clients.manage", "gl.manage"];

    private static async Task<int> CreateAccountAsync(HttpClient client, int cashAccountId, int interestExpenseAccountId, InterestCalculationType calculationType, string label, decimal openingBalance)
    {
        var productRequest = new SaveSavingsProductRequest(
            Name: $"{label} Product", ShortName: label, Description: null, CurrencyId: null, Decimals: 2,
            InterestRate: 7.2m, AllowOverdraft: false, MinimumBalance: 0m,
            InterestCompoundingPeriod: InterestCompoundingPeriod.Monthly, InterestPostingPeriod: InterestPostingPeriod.Monthly,
            InterestCalculationType: calculationType,
            AllowTransferWithdrawalFee: false, OpeningBalance: 0m, AllowAdditionalCharges: true,
            YearDays: SavingsYearDays.Days360, AccountingRule: SavingsAccountingRule.Cash,
            GlAccountSavingsReferenceId: cashAccountId, GlAccountOverdraftPortfolioId: null, GlAccountSavingsControlId: cashAccountId,
            GlAccountInterestOnSavingsId: interestExpenseAccountId, GlAccountSavingsWrittenOffId: null, GlAccountIncomeInterestId: null,
            GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null);
        var product = await (await client.PostAsJsonAsync("/api/v1/savings-products", productRequest)).Content.ReadFromJsonAsync<SavingsProductResponse>(TestJson.Options);

        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/v1/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        var account = await (await client.PostAsJsonAsync("/api/v1/savings-accounts", new OpenSavingsAccountRequest(SavingsClientType.Client, borrower!.Id, null, null, product!.Id, null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);
        var approved = await (await client.PostAsJsonAsync($"/api/v1/savings-accounts/{account!.Id}/approve", new ApproveSavingsAccountRequest(openingBalance, null, new DateOnly(2026, 1, 1), null)))
            .Content.ReadFromJsonAsync<SavingsAccountResponse>(TestJson.Options);

        return approved!.Id;
    }

    private static async Task<int> CreateGlAccountAsync(HttpClient client, string name, string code, GlAccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/gl-accounts", new SaveGlAccountRequest(name, null, code, type, true, null));
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Daily_balance_interest_posts_the_hand_calculated_amount_with_a_balanced_gl_batch()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-interest-daily@bckash.test", AllPermissions);
        var cash = await CreateGlAccountAsync(client, "Savings Control", "2100", GlAccountType.Liability);
        var interestExpense = await CreateGlAccountAsync(client, "Interest On Savings", "5200", GlAccountType.Expense);

        // Constant 10,000 balance for 31 days (2026-01-01 -> 2026-02-01), 7.2%/360 days.
        var accountId = await CreateAccountAsync(client, cash, interestExpense, InterestCalculationType.Daily, "InterestDaily", 10000m);

        var runResponse = await client.PostAsync($"/api/v1/savings-interest/run?asOfDate=2026-02-01", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, runResponse.StatusCode);

        var account = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/v1/savings-accounts/{accountId}", TestJson.Options);
        // 10000 * 0.072 / 360 * 31 = 10000 * 0.0002 * 31 = 62.00
        Assert.Equal(62.00m, account!.InterestEarned);
        Assert.Equal(10062.00m, account.Balance);
        Assert.Equal(new DateOnly(2026, 3, 1), account.NextInterestPostingDate);

        var entries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={interestExpense}", TestJson.Options);
        var interestEntries = entries!.Where(e => e.TransactionType == GlTransactionType.Interest).ToList();
        Assert.NotEmpty(interestEntries);
        Assert.Equal(62.00m, interestEntries.Single().Debit);

        var controlEntries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={cash}", TestJson.Options);
        var interestControlEntry = controlEntries!.Single(e => e.TransactionType == GlTransactionType.Interest);
        Assert.Equal(62.00m, interestControlEntry.Credit);
        Assert.True(interestEntries.Single().Approved);
    }

    [Fact]
    public async Task Average_balance_interest_uses_the_two_point_opening_closing_simplification()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-interest-average@bckash.test", AllPermissions);
        var cash = await CreateGlAccountAsync(client, "Savings Control 2", "2101", GlAccountType.Liability);
        var interestExpense = await CreateGlAccountAsync(client, "Interest On Savings 2", "5201", GlAccountType.Expense);

        var accountId = await CreateAccountAsync(client, cash, interestExpense, InterestCalculationType.Average, "InterestAverage", 10000m);

        // A deposit lands partway through the period — opening 10000, closing 15000.
        await client.PostAsJsonAsync($"/api/v1/savings-accounts/{accountId}/transactions/deposit", new RecordSavingsTransactionRequest(5000m, new DateOnly(2026, 1, 11), "Mid-period deposit"));

        var runResponse = await client.PostAsync($"/api/v1/savings-interest/run?asOfDate=2026-02-01", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, runResponse.StatusCode);

        var account = await client.GetFromJsonAsync<SavingsAccountResponse>($"/api/v1/savings-accounts/{accountId}", TestJson.Options);
        // Average balance (10000+15000)/2 = 12500, over 31 days at 0.0002/day = 12500 * 0.0002 * 31 = 77.50
        Assert.Equal(77.50m, account!.InterestEarned);
    }
}
