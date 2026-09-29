using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>
/// Client loan savings: 2.5% of every repayment on a loan disbursed since savings began, grossed up so the
/// loan still clears on schedule; withdrawn in full after, at 15% less during, or forfeited on write-off.
/// </summary>
public class ClientSavingsTests : IClassFixture<BCKashWebApplicationFactory>
{
    private static readonly string[] Permissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    private readonly BCKashWebApplicationFactory _factory;

    public ClientSavingsTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Repayments_set_aside_savings_and_the_summary_shows_the_whole_loan()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-summary@bckash.test", Permissions);
        var (loanId, clientId) = await CreateDisbursedLoanAsync(staff, "SavingsSummary");

        // 12 × (1,000 + 120): the client pays each 1,120 instalment grossed up to 1,148.72.
        var schedule = await staff.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.All(schedule!, s => Assert.Equal(1148.72m, s.CustomerPays));

        // Paying that clears the first instalment and saves 28.72.
        var paid = await (await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1148.72m, null, new DateOnly(2026, 2, 1), null)))
            .Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(1000m, paid!.Principal);
        Assert.Equal(120m, paid.Interest);

        var summary = await staff.GetFromJsonAsync<LoanSummaryResponse>($"/api/v1/loans/{loanId}/summary", TestJson.Options);
        Assert.Equal(12000m, summary!.Principal);
        Assert.Equal(13440m, summary.ExpectedTotal);
        Assert.Equal(13784.62m, summary.CustomerTotal);
        Assert.Equal(1148.72m, summary.TotalRepaid);
        Assert.Equal(28.72m, summary.SavedFromRepayments);
        Assert.Equal(12320m, summary.TotalRemaining);
        Assert.Equal(new DateOnly(2027, 1, 1), summary.ExpectedCompletionDate);
        Assert.Equal("None", summary.Penalty.Status);

        var savings = await staff.GetFromJsonAsync<ClientSavingsResponse>($"/api/v1/clients/{clientId}/savings", TestJson.Options);
        Assert.Equal(28.72m, savings!.Balance);
        Assert.True(savings.HasRunningLoan);

        // Reversing the repayment takes its savings back out.
        Assert.True((await staff.PostAsync($"/api/v1/loans/{loanId}/repayments/{paid.Id}/reverse", null)).IsSuccessStatusCode);
        Assert.Equal(0m, (await staff.GetFromJsonAsync<ClientSavingsResponse>($"/api/v1/clients/{clientId}/savings", TestJson.Options))!.Balance);
    }

    [Fact]
    public async Task Cashing_out_early_keeps_15_percent_and_afterwards_it_is_paid_in_full()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-withdraw@bckash.test", Permissions);
        var (loanId, clientId) = await CreateDisbursedLoanAsync(staff, "SavingsWithdraw");
        await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(4000m, null, new DateOnly(2026, 2, 1), null));

        // 2.5% of 4,000 = 100 saved; the loan is still running, so cashing out keeps 15.
        var early = await (await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/savings/withdraw", new WithdrawSavingsRequest(null)))
            .Content.ReadFromJsonAsync<SavingsWithdrawalResponse>(TestJson.Options);
        Assert.Equal(85m, early!.Payout);
        Assert.Equal(15m, early.Fee);
        Assert.Equal(0m, early.Savings.Balance);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/savings/withdraw", new WithdrawSavingsRequest(null))).StatusCode);

        // Once no loan is running, the savings come out whole.
        await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(2000m, null, new DateOnly(2026, 3, 1), null));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            (await db.Loans.SingleAsync(l => l.Id == loanId)).Status = LoanStatus.Closed;
            await db.SaveChangesAsync();
        }

        var full = await (await staff.PostAsJsonAsync($"/api/v1/clients/{clientId}/savings/withdraw", new WithdrawSavingsRequest("Loan completed")))
            .Content.ReadFromJsonAsync<SavingsWithdrawalResponse>(TestJson.Options);
        Assert.Equal(50m, full!.Payout);
        Assert.Equal(0m, full.Fee);
    }

    [Fact]
    public async Task Writing_a_loan_off_forfeits_the_savings()
    {
        var staff = await AuthenticatedClientFactory.CreateAsync(_factory, "savings-forfeit@bckash.test", Permissions);
        var (loanId, clientId) = await CreateDisbursedLoanAsync(staff, "SavingsForfeit");
        await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(2000m, null, new DateOnly(2026, 2, 1), null));
        Assert.Equal(50m, (await staff.GetFromJsonAsync<ClientSavingsResponse>($"/api/v1/clients/{clientId}/savings", TestJson.Options))!.Balance);

        Assert.True((await staff.PostAsJsonAsync($"/api/v1/loans/{loanId}/write-off", new WriteOffLoanRequest("Client absconded", null))).IsSuccessStatusCode);

        var savings = await staff.GetFromJsonAsync<ClientSavingsResponse>($"/api/v1/clients/{clientId}/savings", TestJson.Options);
        Assert.Equal(0m, savings!.Balance);
        Assert.Contains(savings.Entries, e => e.Type == ClientSavingsEntryType.Forfeiture && e.Amount == -50m);
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
