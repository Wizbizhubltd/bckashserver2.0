using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

public class LoanRescheduleControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanRescheduleControllerTests(BCKashWebApplicationFactory factory)
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
    public async Task Approval_regenerates_the_schedule_from_the_reschedule_from_date_forward()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "reschedule-approve@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "Reschedule");

        // Pay off installment 1 (due 2026-02-01) so it's settled before the reschedule.
        await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 15), null));

        // Installment 2 is due 2026-03-01 — reschedule everything from that date forward (11 remaining installments).
        var createResponse = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/reschedule-requests",
            new CreateRescheduleRequest(10000m, new DateOnly(2026, 3, 1), true, "Client requested lower payments"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var request = await createResponse.Content.ReadFromJsonAsync<LoanRescheduleRequestResponse>(TestJson.Options);
        Assert.Equal(RescheduleRequestStatus.Pending, request!.Status);

        var approveResponse = await client.PostAsync($"/api/v1/loans/{loanId}/reschedule-requests/{request.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<LoanRescheduleRequestResponse>(TestJson.Options);
        Assert.Equal(RescheduleRequestStatus.Approved, approved!.Status);

        var loan = await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options);
        Assert.Equal(LoanStatus.Rescheduled, loan!.Status);

        var schedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);

        // Installment 1 (already paid, before the reschedule-from date) is untouched.
        var first = schedule!.Single(s => s.Installment == 1);
        Assert.True(first.Paid);
        Assert.Equal(1000m, first.PrincipalPaid);

        // The remaining 11 installments were replaced and now total the new 10,000 principal.
        var remaining = schedule!.Where(s => s.Installment != 1).ToList();
        Assert.Equal(11, remaining.Count);
        Assert.Equal(10000m, remaining.Sum(s => s.Principal ?? 0m));
        Assert.All(remaining, s => Assert.False(s.Paid));
        Assert.All(remaining, s => Assert.True(s.DueDate >= new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public async Task Rejecting_a_reschedule_request_leaves_the_schedule_untouched()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "reschedule-reject@bckash.test", AllPermissions);
        var loanId = await CreateDisbursedLoanAsync(client, "RescheduleReject");

        var scheduleBefore = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);

        var createResponse = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/reschedule-requests",
            new CreateRescheduleRequest(10000m, new DateOnly(2026, 3, 1), true, null));
        var request = await createResponse.Content.ReadFromJsonAsync<LoanRescheduleRequestResponse>(TestJson.Options);

        var rejectResponse = await client.PostAsJsonAsync($"/api/v1/loans/{loanId}/reschedule-requests/{request!.Id}/reject", new ReasonRequest("Not approved by credit committee"));
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<LoanRescheduleRequestResponse>(TestJson.Options);
        Assert.Equal(RescheduleRequestStatus.Rejected, rejected!.Status);

        var scheduleAfter = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/v1/loans/{loanId}/schedule", TestJson.Options);
        Assert.Equal(scheduleBefore!.Count, scheduleAfter!.Count);
        for (var i = 0; i < scheduleBefore.Count; i++)
        {
            Assert.Equal(scheduleBefore[i].Principal, scheduleAfter[i].Principal);
            Assert.Equal(scheduleBefore[i].DueDate, scheduleAfter[i].DueDate);
        }

        var loan = await client.GetFromJsonAsync<LoanResponse>($"/api/v1/loans/{loanId}", TestJson.Options);
        Assert.Equal(LoanStatus.Disbursed, loan!.Status);
    }
}
