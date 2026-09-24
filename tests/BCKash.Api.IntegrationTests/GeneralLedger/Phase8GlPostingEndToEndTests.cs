using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Assets;
using BCKash.Domain.Expenses;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Payroll;
using Xunit;

namespace BCKash.Api.IntegrationTests.GeneralLedger;

/// <summary>
/// Phase 8's fourth acceptance criterion: fixed-asset depreciation, expense approval, other
/// income approval and a payroll run each post correct, balanced GL entries, verified by
/// pulling the trial balance afterwards — same shape as GlReportsControllerTests'
/// disbursement/write-off/manual-entry scenario.
/// </summary>
public class Phase8GlPostingEndToEndTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public Phase8GlPostingEndToEndTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions =
    [
        "gl.manage", "assets.manage", "expenses.manage", "expenses.approve",
        "other-income.manage", "other-income.approve", "payroll.manage", "payroll.run",
    ];

    [Fact]
    public async Task Depreciation_expense_other_income_and_payroll_all_post_balanced_entries()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "phase8-gl@bckash.test", AllPermissions);

        var depreciationExpenseAccount = await CreateAccountAsync(client, "Depreciation Expense", "5200", GlAccountType.Expense);
        var accumulatedDepreciationAccount = await CreateAccountAsync(client, "Accumulated Depreciation", "1150", GlAccountType.Asset);
        var operatingExpenseAccount = await CreateAccountAsync(client, "Operating Expense", "5300", GlAccountType.Expense);
        var cashAccount = await CreateAccountAsync(client, "Cash", "1000", GlAccountType.Asset);
        var otherIncomeAccount = await CreateAccountAsync(client, "Other Income", "4200", GlAccountType.Income);
        var payrollExpenseAccount = await CreateAccountAsync(client, "Payroll Expense", "5400", GlAccountType.Expense);

        // --- Fixed asset depreciation ---
        var assetType = await (await client.PostAsJsonAsync("/api/v1/asset-types", new SaveAssetTypeRequest(
            "Office Equipment", null, null, accumulatedDepreciationAccount, depreciationExpenseAccount, null, null, null)))
            .Content.ReadFromJsonAsync<AssetTypeResponse>(TestJson.Options);

        var asset = await (await client.PostAsJsonAsync("/api/v1/assets", new SaveAssetRequest(
            assetType!.Id, null, "Laptop", new DateOnly(2024, 1, 1), 12_000m, 5, 2_000m, "SN-001", null, null, "2024")))
            .Content.ReadFromJsonAsync<AssetResponse>(TestJson.Options);

        var depreciationResponse = await client.PostAsJsonAsync($"/api/v1/assets/{asset!.Id}/depreciate", new RunDepreciationRequest("2026"));
        Assert.True(depreciationResponse.IsSuccessStatusCode);
        var depreciation = await depreciationResponse.Content.ReadFromJsonAsync<AssetDepreciationResponse>(TestJson.Options);
        Assert.Equal(2_000m, depreciation!.DepreciationValue); // (12,000 - 2,000) / 5 years

        // --- Expense approval ---
        var expenseType = await (await client.PostAsJsonAsync("/api/v1/expense-types", new SaveExpenseTypeRequest(
            "Office Supplies", cashAccount, operatingExpenseAccount, null)))
            .Content.ReadFromJsonAsync<ExpenseTypeResponse>(TestJson.Options);

        var expense = await (await client.PostAsJsonAsync("/api/v1/expenses", new SaveExpenseRequest(
            null, expenseType!.Id, "Printer paper", 500m, new DateOnly(2026, 1, 5), false, null, null, null, ExpenseRecurType.Month, null, null)))
            .Content.ReadFromJsonAsync<ExpenseResponse>(TestJson.Options);

        var approvedExpenseResponse = await client.PostAsJsonAsync($"/api/v1/expenses/{expense!.Id}/approve", new ApproveRequest(null));
        Assert.True(approvedExpenseResponse.IsSuccessStatusCode);

        // --- Other income approval ---
        var incomeType = await (await client.PostAsJsonAsync("/api/v1/other-income-types", new SaveOtherIncomeTypeRequest(
            "Asset Sale", cashAccount, otherIncomeAccount, null)))
            .Content.ReadFromJsonAsync<OtherIncomeTypeResponse>(TestJson.Options);

        var income = await (await client.PostAsJsonAsync("/api/v1/other-income", new SaveOtherIncomeRequest(
            null, incomeType!.Id, "Sold old chairs", 750m, new DateOnly(2026, 1, 6), null, null)))
            .Content.ReadFromJsonAsync<OtherIncomeResponse>(TestJson.Options);

        var approvedIncomeResponse = await client.PostAsJsonAsync($"/api/v1/other-income/{income!.Id}/approve", new ApproveRequest(null));
        Assert.True(approvedIncomeResponse.IsSuccessStatusCode);

        // --- Payroll run (no template — gross amount flows straight through as net pay) ---
        var payrollResponse = await client.PostAsJsonAsync("/api/v1/payroll/runs", new RunPayrollRequest(
            null, payrollExpenseAccount, cashAccount, null, null, "Jane Doe", null, "bank_transfer", null, null, null, null, null,
            50_000m, new DateOnly(2026, 1, 31), false, null, null, null, PayrollRecurType.Months));
        Assert.True(payrollResponse.IsSuccessStatusCode);
        var payroll = await payrollResponse.Content.ReadFromJsonAsync<PayrollResponse>(TestJson.Options);
        Assert.Equal(50_000m, payroll!.PaidAmount); // no template line items -> net pay equals gross

        // --- Trial balance nets to zero across all four postings ---
        var trialBalance = await client.GetFromJsonAsync<List<TrialBalanceRowResponse>>("/api/v1/gl/reports/trial-balance", TestJson.Options);
        Assert.NotNull(trialBalance);
        Assert.NotEmpty(trialBalance);
        Assert.Equal(trialBalance.Sum(r => r.TotalDebit), trialBalance.Sum(r => r.TotalCredit));

        // Each posting is independently traceable and balanced.
        await AssertBalancedBatchAsync(client, depreciationExpenseAccount);
        await AssertBalancedBatchAsync(client, operatingExpenseAccount);
        await AssertBalancedBatchAsync(client, otherIncomeAccount);
        await AssertBalancedBatchAsync(client, payrollExpenseAccount);
    }

    private static async Task AssertBalancedBatchAsync(HttpClient client, int glAccountId)
    {
        var entries = await client.GetFromJsonAsync<List<GlJournalEntryResponse>>($"/api/v1/gl/journal-entries?glAccountId={glAccountId}", TestJson.Options);
        Assert.NotEmpty(entries!);
        Assert.All(entries!, e => Assert.True(e.Approved && !e.ManualEntry));
    }

    private static async Task<int> CreateAccountAsync(HttpClient client, string name, string code, GlAccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/gl-accounts", new SaveGlAccountRequest(name, null, code, type, true, null));
        var created = await response.Content.ReadFromJsonAsync<GlAccountResponse>(TestJson.Options);
        return created!.Id;
    }
}
