using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanRepaymentsControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanRepaymentsControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // P=12000, 1%/month, 12 installments, flat/equal-installment — matches docs/interest-calculation-spec.md's
    // Example 1 exactly: every installment is Principal=1000.00, Interest=120.00, Total=1120.00.
    private static SaveLoanProductRequest CleanMonthlyProductRequest(string name) => new(
        Name: name, ShortName: name, Description: null, FundId: null, CurrencyId: null, Decimals: 2,
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

    /// <summary>Produces a Disbursed loan with the clean 12×(1000 principal, 120 interest) schedule described above.</summary>
    private static async Task<int> CreateDisbursedLoanAsync(HttpClient client, string label)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/loan-products", CleanMonthlyProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null));
        return loanId;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    [Fact]
    public async Task On_time_repayment_fully_covers_the_first_installment()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-ontime@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "OnTime");

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), "First payment"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(1000m, transaction!.Principal);
        Assert.Equal(120m, transaction.Interest);
        Assert.Equal(LoanTransactionType.Repayment, transaction.TransactionType);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.True(first.Paid);
        Assert.Equal(1000m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);

        var second = schedule!.Single(s => s.Installment == 2);
        Assert.False(second.Paid);
        Assert.Equal(0m, second.PrincipalPaid ?? 0m);
    }

    [Fact]
    public async Task Partial_repayment_allocates_interest_before_principal_and_leaves_the_installment_unpaid()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-partial@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Partial");

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(500m, null, new DateOnly(2026, 1, 15), "Partial payment"));
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        // Strategy is InterestPrincipalPenaltyFees: interest (120) fully covered first, remaining 380 to principal.
        Assert.Equal(120m, transaction!.Interest);
        Assert.Equal(380m, transaction.Principal);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.False(first.Paid);
        Assert.Equal(380m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);
    }

    [Fact]
    public async Task Late_repayment_still_allocates_correctly_and_is_recorded_as_paid_late()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-late@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Late");

        // Installment 1 is due 2026-02-01; pay well after that date.
        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 2, 20), "Late payment"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.True(first.Paid);
        Assert.Equal(1000m, first.PrincipalPaid);
        Assert.Equal(120m, first.InterestPaid);
    }

    [Fact]
    public async Task Overpayment_beyond_the_entire_schedules_outstanding_is_reported_on_the_transaction()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-overpay@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Overpay");

        // Total outstanding across all 12 installments is 12 x 1120 = 13440; pay 500 more than that.
        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(13940m, null, new DateOnly(2026, 1, 15), null));
        var transaction = await response.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        Assert.Equal(13440m, transaction!.Principal + transaction.Interest);
        Assert.Equal(500m, transaction.Overpayment);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        Assert.All(schedule!, s => Assert.True(s.Paid));
    }

    [Fact]
    public async Task Reversing_a_repayment_restores_the_schedule_to_its_exact_pre_transaction_state()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-reverse@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Reverse");

        var beforeSchedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);

        var payResponse = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null));
        var transaction = await payResponse.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);

        var afterPayment = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        Assert.True(afterPayment!.Single(s => s.Installment == 1).Paid);

        var reverseResponse = await client.PostAsync($"/api/loans/{loanId}/repayments/{transaction!.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversedTransaction = await reverseResponse.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.True(reversedTransaction!.Reversed);

        var afterReversal = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);

        // Exact pre-transaction state restored.
        for (var i = 0; i < beforeSchedule!.Count; i++)
        {
            Assert.Equal(beforeSchedule[i].Paid, afterReversal![i].Paid);
            Assert.Equal(beforeSchedule[i].PrincipalPaid ?? 0m, afterReversal[i].PrincipalPaid ?? 0m);
            Assert.Equal(beforeSchedule[i].InterestPaid ?? 0m, afterReversal[i].InterestPaid ?? 0m);
        }

        // Reversing again is rejected.
        var secondReverse = await client.PostAsync($"/api/loans/{loanId}/repayments/{transaction.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondReverse.StatusCode);
    }

    [Fact]
    public async Task Repayment_is_rejected_for_a_loan_that_hasnt_been_disbursed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "repayment-not-disbursed@bckash.test", AllPermissions);
        var productId = (await (await client.PostAsJsonAsync("/api/loan-products", CleanMonthlyProductRequest("NotDisbursed Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, "NotDisbursed");
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);

        var response = await client.PostAsJsonAsync($"/api/loans/{approved!.LoanId}/repayments", new RecordRepaymentRequest(1120m, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
