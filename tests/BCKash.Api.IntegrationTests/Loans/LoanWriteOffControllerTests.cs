using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanWriteOffControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanWriteOffControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

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
        var response = await client.PostAsJsonAsync("/api/v1/clients", request);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);
        return created!.Id;
    }

    private static async Task<int> CreateDisbursedLoanAsync(HttpClient client, string label)
    {
        var productId = (await (await client.PostAsJsonAsync("/api/v1/loan-products", CleanMonthlyProductRequest($"{label} Product"))).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options))!.Id;
        var clientId = await CreateClientAsync(client, label);
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, clientId, null, productId, 12000, 12, FrequencyType.Months, null);
        var application = await (await client.PostAsJsonAsync("/api/v1/loan-applications", applicationRequest)).Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var approveResponse = await client.PostAsJsonAsync($"/api/v1/loan-applications/{application!.Id}/approve", new ApproveLoanApplicationRequest(12000, null));
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approved!.LoanId!.Value;

        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000, null));
        return loanId;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    [Fact]
    public async Task Write_off_moves_all_outstanding_amounts_out_of_the_active_portfolio()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "writeoff-basic@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "WriteOff");

        // Pay off the first installment; the remaining 11 x 1120 = 12320 stays outstanding.
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null));

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/write-off", new WriteOffLoanRequest("Client unreachable, deemed unrecoverable", new DateOnly(2026, 6, 1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(TestJson.Options);
        Assert.Equal(LoanStatus.WrittenOff, loan!.Status);
        Assert.Equal("Client unreachable, deemed unrecoverable", loan.WrittenOffNotes);
        Assert.NotNull(loan.WrittenOffDate);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.All(schedule!, s => Assert.True(s.Paid)); // resolved (via write-off), nothing left outstanding
        var unpaidInstallment = schedule!.Single(s => s.Installment == 2);
        Assert.Equal(1000m, unpaidInstallment.PrincipalWrittenOff);
        Assert.Equal(120m, unpaidInstallment.InterestWrittenOff);

        var transactions = await client.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/v1/loans/{loanId}/repayments", TestJson.Options);
        var writeOffTransaction = transactions!.Single(t => t.TransactionType == LoanTransactionType.WriteOff);
        Assert.Equal(12320m, writeOffTransaction.Principal + writeOffTransaction.Interest);
    }

    [Fact]
    public async Task Recovery_after_write_off_is_recorded_as_its_own_transaction()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "writeoff-recovery@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Recovery");
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/write-off", new WriteOffLoanRequest("Unrecoverable", null));

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/record-recovery", new RecordRecoveryRequest(2000m, new DateOnly(2026, 8, 1), "Partial recovery via collections agency"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await client.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/v1/loans/{loanId}/repayments", TestJson.Options);
        var recovery = transactions!.Single(t => t.TransactionType == LoanTransactionType.WriteOffRecovery);
        Assert.Equal(2000m, recovery.Amount);
    }

    [Fact]
    public async Task Write_off_requires_the_loan_to_be_disbursed()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "writeoff-invalid@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "WriteOffInvalid");
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/write-off", new WriteOffLoanRequest("First write-off", null));

        var secondWriteOff = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/write-off", new WriteOffLoanRequest("Second attempt", null));

        Assert.Equal(HttpStatusCode.BadRequest, secondWriteOff.StatusCode);
    }

    [Fact]
    public async Task Recovery_requires_the_loan_to_already_be_written_off()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "writeoff-recovery-invalid@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "RecoveryInvalid");

        var response = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/record-recovery", new RecordRecoveryRequest(1000m, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
