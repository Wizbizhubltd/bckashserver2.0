using System.Net;
using System.Net.Http.Json;
using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests.Dashboard;

public class DashboardControllerTests : IClassFixture<BCKashWebApplicationFactory>
{
    private readonly BCKashWebApplicationFactory _factory;

    public DashboardControllerTests(BCKashWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Loan_portfolio_totals_amounts_by_stage()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "dashboard-portfolio@bckash.test", "organization.manage");
        // Tests in this class share a database, so compare against what was there before.
        var before = await PortfolioAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.AddRange(
                // Legacy loans still awaiting approval (no approval recorded).
                new Loan { Status = LoanStatus.Pending, AppliedAmount = 100 },
                new Loan { Status = LoanStatus.NeedChanges, AppliedAmount = 50 },
                // Created by approving an application: Pending here means approved, awaiting disbursement.
                new Loan { Status = LoanStatus.Pending, AppliedAmount = 150, ApprovedAmount = 120, ApprovedDate = DateOnly.FromDateTime(DateTime.UtcNow) },
                new Loan { Status = LoanStatus.Declined, AppliedAmount = 30 },
                new Loan { Status = LoanStatus.Rejected, Principal = 20 },
                new Loan { Status = LoanStatus.Approved, AppliedAmount = 400, ApprovedAmount = 300 },
                new Loan { Status = LoanStatus.Paid, AppliedAmount = 200, ApprovedAmount = 200 });
            db.LoanTransactions.AddRange(
                new LoanTransaction { TransactionType = LoanTransactionType.Repayment, Amount = 75 },
                new LoanTransaction { TransactionType = LoanTransactionType.Repayment, Amount = 999, Reversed = true });

            var product = new LoanProduct { Name = "Dashboard Test Product" };
            db.LoanProducts.Add(product);
            await db.SaveChangesAsync();

            // An approved application already became a loan, so it isn't counted a second time.
            db.LoanApplications.AddRange(
                new LoanApplication { LoanProductId = product.Id, Status = ApprovalStatus.Pending, Amount = 70 },
                new LoanApplication { LoanProductId = product.Id, Status = ApprovalStatus.Declined, Amount = 40 },
                new LoanApplication { LoanProductId = product.Id, Status = ApprovalStatus.Approved, Amount = 999 });
            await db.SaveChangesAsync();
        }

        var after = await PortfolioAsync(client);

        Assert.Equal(1060m, after.RequestedAmount - before.RequestedAmount);
        Assert.Equal(9, after.RequestedCount - before.RequestedCount);
        Assert.Equal(620m, after.ApprovedAmount - before.ApprovedAmount);
        Assert.Equal(3, after.ApprovedCount - before.ApprovedCount);
        Assert.Equal(90m, after.RejectedAmount - before.RejectedAmount);
        Assert.Equal(3, after.RejectedCount - before.RejectedCount);
        Assert.Equal(220m, after.PendingApprovalAmount - before.PendingApprovalAmount);
        Assert.Equal(3, after.PendingApprovalCount - before.PendingApprovalCount);
        Assert.Equal(75m, after.RepaidAmount - before.RepaidAmount);
        Assert.Equal(1, after.RepaidCount - before.RepaidCount);
    }

    [Fact]
    public async Task Late_and_defaulted_loans_are_split_by_the_final_repayment_date()
    {
        var client = await AuthenticatedClientFactory.CreateAsync(_factory, "dashboard-defaulted@bckash.test", "organization.manage");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var before = await PortfolioAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BCKashDbContext>();
            db.Loans.AddRange(
                // Maturity date passed, one instalment partly unpaid: 60 principal + 5 interest outstanding.
                DisbursedLoan(today.AddDays(-10),
                    Instalment(today.AddMonths(-2), 100, 100, 10, 10),
                    Instalment(today.AddDays(-10), 100, 40, 10, 5)),
                // No maturity date recorded — the last instalment's due date has passed: 50 outstanding.
                DisbursedLoan(null, Instalment(today.AddDays(-3), 50, 0, 0, 0)),
                // Late, not defaulted: an instalment is overdue (100 + 8 interest), the final one isn't due yet.
                DisbursedLoan(null,
                    Instalment(today.AddDays(-5), 100, 0, 8, 0),
                    Instalment(today.AddMonths(1), 100, 0, 8, 0)),
                // Late with a recorded maturity date still ahead: 30 overdue.
                DisbursedLoan(today.AddMonths(2), Instalment(today.AddDays(-1), 30, 0, 0, 0)),
                // Up to date: nothing overdue yet.
                DisbursedLoan(null, Instalment(today.AddDays(5), 100, 0, 0, 0)),
                // Past maturity but fully paid.
                DisbursedLoan(today.AddDays(-30), Instalment(today.AddDays(-30), 100, 100, 0, 0)));
            await db.SaveChangesAsync();
        }

        var after = await PortfolioAsync(client);

        Assert.Equal(2, after.LateRepaymentCount - before.LateRepaymentCount);
        Assert.Equal(138m, after.LateRepaymentAmount - before.LateRepaymentAmount);
        Assert.Equal(2, after.DefaultedCount - before.DefaultedCount);
        Assert.Equal(115m, after.DefaultedAmount - before.DefaultedAmount);
    }

    private static async Task<LoanPortfolioSummary> PortfolioAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>())!.LoanPortfolio;
    }

    private static Loan DisbursedLoan(DateOnly? maturity, params LoanRepaymentSchedule[] instalments)
    {
        var loan = new Loan { Status = LoanStatus.Disbursed, ExpectedMaturityDate = maturity };
        foreach (var instalment in instalments)
        {
            loan.LoanRepaymentSchedules.Add(instalment);
        }

        return loan;
    }

    private static LoanRepaymentSchedule Instalment(DateOnly due, decimal principal, decimal principalPaid, decimal interest, decimal interestPaid) =>
        new() { DueDate = due, Principal = principal, PrincipalPaid = principalPaid, Interest = interest, InterestPaid = interestPaid };
}
