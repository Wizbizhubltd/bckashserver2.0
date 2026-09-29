using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>
/// Settings → Loan → Client savings: the savings share of each repayment and the early cash-out charge. Its own
/// fixture — changing them changes savings for every test sharing the database.
/// </summary>
public class ClientSavingsSettingsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private static readonly string[] Permissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    private readonly BCKashWebApplicationFactory _factory;

    public ClientSavingsSettingsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_savings_share_and_early_cash_out_charge_follow_the_settings()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-settings@bckash.test", Permissions);
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, "savings-settings-sa@bckash.test");

        // Disbursed at the default 2.5%.
        var (earlierLoanId, _) = await CreateDisbursedLoanAsync(staff, "SettingsEarlier");

        Assert.Equal(HttpStatusCode.Created, (await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(ClientSavingsSettingKeys.Rate, "5"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(ClientSavingsSettingKeys.EarlyWithdrawalFee, "10"))).StatusCode);

        // A loan disbursed now saves 5%: 1,120 grossed up is 1,120 ÷ 0.95 = 1,178.95.
        var (loanId, clientId) = await CreateDisbursedLoanAsync(staff, "SettingsNew");
        var schedule = await staff.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.All(schedule!, s => Assert.Equal(1178.95m, s.CustomerPays));
        await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(2000m, null, new DateOnly(2026, 2, 1), null));
        Assert.Equal(100m, (await staff.GetFromJsonAsync<LoanSummaryResponse>($"/api/v1/loans/{loanId}/summary", TestJson.Options))!.SavedFromRepayments);

        // The loan disbursed earlier keeps the 2.5% its schedule was grossed up for.
        Assert.All((await staff.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{earlierLoanId}/schedule", TestJson.Options))!, s => Assert.Equal(1148.72m, s.CustomerPays));

        // Cashing out early now keeps 10%.
        var savings = await staff.GetFromJsonAsync<ClientSavingsResponse>($"/api/v1/clients/{clientId}/savings", TestJson.Options);
        Assert.Equal(0.10m, savings!.EarlyWithdrawalFeeRate);
        var early = await (await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/savings/withdraw", new WithdrawSavingsRequest(null)))
            .Content.ReadFromJsonAsync<SavingsWithdrawalResponse>(TestJson.Options);
        Assert.Equal(90m, early!.Payout);
        Assert.Equal(10m, early.Fee);
    }

    [Theory]
    [InlineData(ClientSavingsSettingKeys.Rate, "51")]
    [InlineData(ClientSavingsSettingKeys.Rate, "-1")]
    [InlineData(ClientSavingsSettingKeys.EarlyWithdrawalFee, "101")]
    [InlineData(ClientSavingsSettingKeys.EarlyWithdrawalFee, "lots")]
    public async Task Percentages_must_be_in_range(string key, string value)
    {
        var superAdmin = await AuthenticatedClientFactory.CreateSuperAdminAsync(_factory, $"savings-settings-invalid-{Guid.NewGuid():N}@bckash.test");

        var response = await superAdmin.PostAsJsonAsync("/api/v1/settings", new CreateSettingRequest(key, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A 12,000 loan at 1% a month over 12 months (12 × 1,000 + 120), disbursed 1 Jan 2026 — so it saves.</summary>
    private static async Task<(int LoanId, int ClientId)> CreateDisbursedLoanAsync(HttpClient staff, string label)
    {
        var productId = (await (await staff.PostAsJsonAsync("/api/v1/loan-products", new SaveLoanProductRequest(
            Name: $"{label} Product", ShortName: label, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
            MinimumPrincipal: 1000, DefaultPrincipal: 12000, MaximumPrincipal: 20000,
            MinimumLoanTerm: 6, DefaultLoanTerm: 12, MaximumLoanTerm: 24,
            RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
            MinimumInterestRate: 1, DefaultInterestRate: 1, MaximumInterestRate: 1, InterestRateType: InterestRateFrequencyType.Month,
            GraceOnInterestCharged: null, GraceOnPrincipal: null, GraceOnInterestPayment: null,
            AllowCustomGrace: false, AllowStandingInstructions: false,
            InterestMethod: LoanInterestMethod.Flat, AmortizationMethod: LoanAmortizationMethod.EqualInstallment,
            InterestCalculationPeriodType: InterestCalculationPeriodType.Same, YearDays: YearDaysType.Days365, MonthDays: MonthDaysType.Days30,
            LoanTransactionStrategy: LoanTransactionStrategy.InterestPrincipalPenaltyFees,
            IncludeInCycle: false, LockGuarantee: false, AllocateOverpayments: false, AllowAdditionalCharges: false,
            AccountingRule: LoanAccountingRule.Cash, NpaDays: 90, ArrearsGraceDays: null, NpaSuspendIncome: true,
            GlAccountFundSourceId: null, GlAccountLoanPortfolioId: null, GlAccountReceivableInterestId: null, GlAccountReceivableFeeId: null,
            GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null, GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null,
            GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null)))
            .Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = (await (await staff.PostAsJsonAsync("/api/v1/clients", new CreateClientRequest(
            null, null, null, null, null, null, null, label, null, "Client", $"{label} Client",
            null, $"{label} Client", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null))).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options))!.Id;

        var application = await (await staff.PostAsJsonAsync("/api/v1/loan-applications",
            new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approved = await (await staff.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null)))
            .Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.True((await staff.PostAsJsonAsync($"/api/v1/loans/{approved!.LoanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null))).IsSuccessStatusCode);
        return (approved.LoanId!.Value, clientId);
    }
}
