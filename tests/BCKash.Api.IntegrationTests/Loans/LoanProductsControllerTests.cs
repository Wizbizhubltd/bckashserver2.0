using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanProductsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanProductsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static SaveLoanProductRequest NewProductRequest(
        string name,
        decimal? minPrincipal = 1000, decimal? defaultPrincipal = 5000, decimal? maxPrincipal = 10000,
        int? minTerm = 6, int? defaultTerm = 12, int? maxTerm = 24,
        decimal? minRate = 10, decimal? defaultRate = 15, decimal? maxRate = 20) =>
        new(
            Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
            MinimumPrincipal: minPrincipal, DefaultPrincipal: defaultPrincipal, MaximumPrincipal: maxPrincipal,
            MinimumLoanTerm: minTerm, DefaultLoanTerm: defaultTerm, MaximumLoanTerm: maxTerm,
            RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
            MinimumInterestRate: minRate, DefaultInterestRate: defaultRate, MaximumInterestRate: maxRate,
            InterestRateType: InterestRateFrequencyType.Year,
            GraceOnInterestCharged: null, GraceOnPrincipal: null, GraceOnInterestPayment: null,
            AllowCustomGrace: false, AllowStandingInstructions: false,
            InterestMethod: LoanInterestMethod.Flat, AmortizationMethod: LoanAmortizationMethod.EqualInstallment,
            InterestCalculationPeriodType: InterestCalculationPeriodType.Same, YearDays: YearDaysType.Days365, MonthDays: MonthDaysType.Days30,
            LoanTransactionStrategy: LoanTransactionStrategy.InterestPrincipalPenaltyFees,
            IncludeInCycle: false, LockGuarantee: false, AllocateOverpayments: false, AllowAdditionalCharges: false,
            AccountingRule: LoanAccountingRule.Cash, NpaDays: null, ArrearsGraceDays: null, NpaSuspendIncome: false,
            GlAccountFundSourceId: null, GlAccountLoanPortfolioId: null, GlAccountReceivableInterestId: null, GlAccountReceivableFeeId: null,
            GlAccountReceivablePenaltyId: null, GlAccountLoanOverPaymentsId: null, GlAccountSuspendedIncomeId: null, GlAccountIncomeInterestId: null,
            GlAccountIncomeFeeId: null, GlAccountIncomePenaltyId: null, GlAccountIncomeRecoveryId: null, GlAccountLoansWrittenOffId: null);

    [Fact]
    public async Task Create_and_fetch_a_loan_product()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "product-crud@bckash.test", "loan-products.manage");

        var response = await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Standard Loan"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        Assert.Equal("Standard Loan", created!.Name);
        Assert.True(created.Active);
    }

    [Theory]
    [InlineData(5000, 1000, 10000)] // min > default (principal)
    [InlineData(1000, 15000, 10000)] // default > max (principal)
    public async Task Create_rejects_principal_min_default_max_violations(decimal min, decimal @default, decimal max)
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, $"product-range-{min}-{@default}-{max}@bckash.test", "loan-products.manage");

        var response = await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Bad Range", minPrincipal: min, defaultPrincipal: @default, maxPrincipal: max));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_term_min_default_max_violation()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "product-range-term@bckash.test", "loan-products.manage");

        var response = await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Bad Term", minTerm: 24, defaultTerm: 12, maxTerm: 6));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_interest_rate_min_default_max_violation()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "product-range-rate@bckash.test", "loan-products.manage");

        var response = await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Bad Rate", minRate: 20, defaultRate: 15, maxRate: 10));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_is_blocked_once_a_loan_exists_against_the_product_but_allowed_before()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "product-delete@bckash.test", "loan-products.manage");
        var created = await (await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Deletable"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        // No loan yet — hard delete allowed.
        var earlyDelete = await client.DeleteAsync($"/api/loan-products/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, earlyDelete.StatusCode);

        var inUseProduct = await (await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("In Use"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.Add(new Loan { LoanProductId = inUseProduct!.Id, AccountNumber = $"SEED-{Guid.NewGuid():N}" });
            await db.SaveChangesAsync();
        }

        var blockedDelete = await client.DeleteAsync($"/api/loan-products/{inUseProduct!.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode);
    }

    [Fact]
    public async Task Activate_and_deactivate_toggle_the_Active_flag()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "product-activate@bckash.test", "loan-products.manage");
        var created = await (await client.PostAsJsonAsync("/api/loan-products", NewProductRequest("Togglable"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        var deactivateResponse = await client.PostAsync($"/api/loan-products/{created!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        Assert.False(deactivated!.Active);

        var activateResponse = await client.PostAsync($"/api/loan-products/{created.Id}/activate", null);
        var activated = await activateResponse.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        Assert.True(activated!.Active);
    }
}
