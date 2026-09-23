using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using Xunit;

namespace BCKash.Api.IntegrationTests.Loans;

/// <summary>
/// The acceptance-criterion end-to-end test: create client → apply for loan → approve → disburse
/// → several repayments (on-time, late, partial) → confirm final schedule/balances match
/// hand-calculated expectations. Uses the same clean P=12000, 1%/month, 12-installment product as
/// docs/interest-calculation-spec.md's Example 1 (every installment: Principal 1000.00, Interest
/// 120.00, Total 1120.00) so every expected figure below is hand-derivable.
/// </summary>
public class LoanServicingEndToEndTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public LoanServicingEndToEndTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] AllPermissions =
        ["loan-applications.manage", "loan-applications.approve", "loan-products.manage", "clients.manage", "loan-servicing.manage"];

    [Fact]
    public async Task Client_to_loan_to_disbursement_to_mixed_repayments_matches_hand_calculated_expectations()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "e2e-servicing@bckash.test", AllPermissions);

        // 1. Client.
        var clientRequest = new CreateClientRequest(
            null, null, null, null, null, null, null, "EndToEnd", null, "Borrower", "EndToEnd Borrower",
            null, "EndToEnd Borrower", null, null, null, null, null, ClientType.Individual, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var borrower = await (await client.PostAsJsonAsync("/api/clients", clientRequest)).Content.ReadFromJsonAsync<ClientResponse>(TestJson.Options);

        // 2. Loan product: P=12000 default, 1%/month flat/equal-installment, 12 monthly installments.
        var productRequest = new SaveLoanProductRequest(
            Name: "E2E Product", ShortName: "E2E", Description: null, FundId: null, CurrencyId: null, Decimals: 2,
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
        var product = await (await client.PostAsJsonAsync("/api/loan-products", productRequest)).Content.ReadFromJsonAsync<LoanProductResponse>(TestJson.Options);

        // 3. Apply for the loan.
        var applicationRequest = new CreateLoanApplicationRequest(LoanClientType.Client, null, null, null, borrower!.Id, null, product!.Id, 12000m, 12, FrequencyType.Months, "End-to-end test loan");
        var applicationResponse = await client.PostAsJsonAsync("/api/loan-applications", applicationRequest);
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var application = await applicationResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        Assert.Equal(ApprovalStatus.Pending, application!.Status);

        // 4. Approve.
        var approveResponse = await client.PostAsJsonAsync($"/api/loan-applications/{application.Id}/approve", new ApproveLoanApplicationRequest(12000m, "Approved for end-to-end test"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approvedApplication = await approveResponse.Content.ReadFromJsonAsync<LoanApplicationResponse>(TestJson.Options);
        var loanId = approvedApplication!.LoanId!.Value;

        // 5. Disburse — generates the schedule.
        var disburseResponse = await client.PostAsJsonAsync($"/api/loans/{loanId}/disburse", new DisburseLoanRequest(new DateOnly(2026, 1, 1), 12000m, "Disbursed for end-to-end test"));
        Assert.Equal(HttpStatusCode.OK, disburseResponse.StatusCode);
        var disbursedLoan = await disburseResponse.Content.ReadFromJsonAsync<LoanResponse>(TestJson.Options);
        Assert.Equal(LoanStatus.Disbursed, disbursedLoan!.Status);

        var initialSchedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        Assert.Equal(12, initialSchedule!.Count);
        Assert.All(initialSchedule, s => Assert.Equal(1000m, s.Principal));
        Assert.All(initialSchedule, s => Assert.Equal(120m, s.Interest));

        // 6. On-time repayment for installment 1 (due 2026-02-01), paid on time.
        var onTime = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 1, 28), "On-time"));
        Assert.Equal(HttpStatusCode.Created, onTime.StatusCode);

        // 7. Late repayment for installment 2 (due 2026-03-01), paid three weeks late.
        var late = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(1120m, null, new DateOnly(2026, 3, 22), "Late"));
        Assert.Equal(HttpStatusCode.Created, late.StatusCode);

        // 8. Two partial repayments covering installment 3 (600 then the remaining 520).
        var partial1 = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(600m, null, new DateOnly(2026, 4, 5), "Partial 1"));
        Assert.Equal(HttpStatusCode.Created, partial1.StatusCode);
        var partial1Transaction = await partial1.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(120m, partial1Transaction!.Interest); // interest-first strategy
        Assert.Equal(480m, partial1Transaction.Principal);

        var partial2 = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RecordRepaymentRequest(520m, null, new DateOnly(2026, 4, 20), "Partial 2"));
        Assert.Equal(HttpStatusCode.Created, partial2.StatusCode);
        var partial2Transaction = await partial2.Content.ReadFromJsonAsync<LoanTransactionResponse>(TestJson.Options);
        Assert.Equal(0m, partial2Transaction!.Interest ?? 0m); // installment 3's interest was already covered by Partial 1
        Assert.Equal(520m, partial2Transaction.Principal);

        // 9. Final schedule/balance verification — hand-calculated expectations.
        var finalSchedule = await client.GetFromJsonAsync<List<ScheduleInstallmentResponse>>($"/api/loans/{loanId}/schedule", TestJson.Options);
        Assert.Equal(12, finalSchedule!.Count);

        foreach (var installmentNumber in new[] { 1, 2, 3 })
        {
            var line = finalSchedule.Single(s => s.Installment == installmentNumber);
            Assert.True(line.Paid);
            Assert.Equal(1000m, line.PrincipalPaid);
            Assert.Equal(120m, line.InterestPaid);
        }

        foreach (var line in finalSchedule.Where(s => s.Installment > 3))
        {
            Assert.False(line.Paid);
            Assert.Equal(0m, line.PrincipalPaid ?? 0m);
            Assert.Equal(0m, line.InterestPaid ?? 0m);
        }

        var totalPaid = finalSchedule.Sum(s => (s.PrincipalPaid ?? 0m) + (s.InterestPaid ?? 0m));
        Assert.Equal(3360m, totalPaid); // 3 x 1120

        var totalOutstanding = finalSchedule.Where(s => !s.Paid).Sum(s => (s.Principal ?? 0m) + (s.Interest ?? 0m));
        Assert.Equal(10080m, totalOutstanding); // 9 x 1120

        // Every recorded repayment transaction is retrievable and none were left reversed.
        var transactions = await client.GetFromJsonAsync<List<LoanTransactionResponse>>($"/api/loans/{loanId}/repayments", TestJson.Options);
        var repayments = transactions!.Where(t => t.TransactionType == LoanTransactionType.Repayment).ToList();
        Assert.Equal(4, repayments.Count);
        Assert.All(repayments, t => Assert.False(t.Reversed));
        Assert.Equal(3360m, repayments.Sum(t => t.Amount ?? 0m));
    }
}
