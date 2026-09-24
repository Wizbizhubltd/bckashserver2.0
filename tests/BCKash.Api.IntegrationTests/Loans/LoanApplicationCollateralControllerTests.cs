using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanApplicationCollateralControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanApplicationCollateralControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static SaveLoanProductRequest NewProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
        MinimumPrincipal: 1000, DefaultPrincipal: 5000, MaximumPrincipal: 10000,
        MinimumLoanTerm: 6, DefaultLoanTerm: 12, MaximumLoanTerm: 24,
        RepaymentFrequency: 1, RepaymentFrequencyType: FrequencyType.Months,
        MinimumInterestRate: 10, DefaultInterestRate: 15, MaximumInterestRate: 20, InterestRateType: InterestRateFrequencyType.Year,
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

    private static async Task<int> CreateProductAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/loan-products", NewProductRequest(name));
        var created = await response.Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);
        return created!.Id;
    }

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

    private static async Task<int> CreateApplicationAsync(HttpClient client, int productId, int clientId)
    {
        var request = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 5000, 12, FrequencyType.Months, null);
        var response = await client.PostAsJsonAsync("/api/v1/loan-applications", request);
        var created = await response.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Attach_a_collateral_item_and_retrieve_it_and_it_stays_attached_after_approval()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "collateral-crud@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage"]);
        var productId = await CreateProductAsync(client, "Collateral Product");
        var clientId = await CreateClientAsync(client, "CollateralApplicant");
        var applicationId = await CreateApplicationAsync(client, productId, clientId);

        var createResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{applicationId}/collateral",
            new SaveCollateralRequest(null, null, "Toyota Camry", "VIN12345", 3000, "2018 sedan"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CollateralResponse>(TestJson.Options);
        Assert.Equal("Toyota Camry", created!.Name);
        Assert.Equal(applicationId, created.LoanApplicationId);
        Assert.Null(created.LoanId);

        var listResponse = await client.GetFromJsonAsync<List<CollateralResponse>>($"/api/v1/loan-applications/{applicationId}/collateral", TestJson.Options);
        Assert.Single(listResponse!);

        await client.PostAsJsonAsync($"/api/v1/loan-applications/{applicationId}/approve", new ApproveLoanApplicationRequest(4500, null));

        var afterApproval = await client.GetFromJsonAsync<List<CollateralResponse>>($"/api/v1/loan-applications/{applicationId}/collateral", TestJson.Options);
        Assert.Single(afterApproval!);
    }

    [Fact]
    public async Task Collateral_for_a_nonexistent_application_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "collateral-404@bckash.test", "loan-applications.manage");

        var response = await client.GetAsync("/api/v1/loan-applications/999999/collateral");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
