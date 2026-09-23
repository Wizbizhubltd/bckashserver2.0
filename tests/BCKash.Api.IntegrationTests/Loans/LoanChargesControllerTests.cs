using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanChargesControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanChargesControllerTests(BCKashWebApplicationFactory factory)
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

    private static async Task<int> CreateApprovedLoanAsync(HttpClient client, string label)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/loan-products", NewProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 5000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(4500, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        return approved!.LoanId!.Value;
    }

    private static async Task<int> CreateLoanScopedChargeAsync(HttpClient client, string name)
    {
        var request = new SaveChargeRequest(
            Name: name, CurrencyId: null, Product: ChargeProduct.Loan, ChargeType: ChargeType.Disbursement, ChargeOption: ChargeOption.Flat,
            ChargeFrequency: 1, ChargeFrequencyType: ChargeFrequencyType.Months, ChargeFrequencyAmount: 1,
            Amount: 100, MinimumAmount: null, MaximumAmount: null, ChargePaymentMode: ChargePaymentMode.Regular,
            Penalty: false, Override: false, GlAccountIncomeId: null);
        var response = await client.PostAsJsonAsync("/api/charges", request);
        var created = await response.Content.ReadFromJsonAsync<ChargeResponse>(TestJson.Options);
        return created!.Id;
    }

    [Fact]
    public async Task Attach_a_loan_charge_and_retrieve_it()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loancharge-crud@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage", "organization.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "Charge");
        var chargeId = await CreateLoanScopedChargeAsync(client, "Disbursement Fee");

        var createResponse = await client.PostAsJsonAsync($"/api/loans/{loanId}/charges",
            new SaveLoanChargeRequest(chargeId, false, LoanChargeType.Disbursement, LoanChargeCalculationType.Flat, 100, null, 0));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LoanChargeResponse>(TestJson.Options);
        Assert.Equal(100, created!.Amount);
        Assert.Equal(loanId, created.LoanId);

        var listResponse = await client.GetFromJsonAsync<List<LoanChargeResponse>>($"/api/loans/{loanId}/charges", TestJson.Options);
        Assert.Single(listResponse!);

        var updateResponse = await client.PutAsJsonAsync($"/api/loans/{loanId}/charges/{created.Id}",
            new SaveLoanChargeRequest(chargeId, false, LoanChargeType.Disbursement, LoanChargeCalculationType.Flat, 150, null, 0));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<LoanChargeResponse>(TestJson.Options);
        Assert.Equal(150, updated!.Amount);

        var deleteResponse = await client.DeleteAsync($"/api/loans/{loanId}/charges/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Attaching_a_charge_scoped_to_a_different_product_is_rejected()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loancharge-wrong-product@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage", "organization.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "WrongProduct");

        var savingsChargeRequest = new SaveChargeRequest(
            Name: "Savings Fee", CurrencyId: null, Product: ChargeProduct.Savings, ChargeType: ChargeType.SavingsActivation, ChargeOption: ChargeOption.Flat,
            ChargeFrequency: 1, ChargeFrequencyType: ChargeFrequencyType.Months, ChargeFrequencyAmount: 1,
            Amount: 50, MinimumAmount: null, MaximumAmount: null, ChargePaymentMode: ChargePaymentMode.Regular,
            Penalty: false, Override: false, GlAccountIncomeId: null);
        var savingsCharge = await (await client.PostAsJsonAsync("/api/charges", savingsChargeRequest)).Content.ReadFromJsonAsync<ChargeResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/charges",
            new SaveLoanChargeRequest(savingsCharge!.Id, false, LoanChargeType.Disbursement, LoanChargeCalculationType.Flat, 50, null, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_loan_charge_can_be_recorded_without_referencing_an_existing_Charge_row()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loancharge-no-ref@bckash.test",
            ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"]);
        var loanId = await CreateApprovedLoanAsync(client, "NoRef");

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/charges",
            new SaveLoanChargeRequest(null, true, LoanChargeType.OverdueInstallmentFee, LoanChargeCalculationType.Flat, 25, null, 5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LoanChargeResponse>(TestJson.Options);
        Assert.Null(created!.ChargeId);
        Assert.True(created.Penalty);
    }

    [Fact]
    public async Task Charges_for_a_nonexistent_loan_return_404()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "loancharge-404@bckash.test", "loan-servicing.manage");

        var response = await client.GetAsync("/api/loans/999999/charges");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
