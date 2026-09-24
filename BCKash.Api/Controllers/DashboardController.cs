using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>Aggregate record counts for the control portal's dashboard — read-only, so any authenticated staff member can view it (no permission policy beyond being logged in).</summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly BCKashDbContext _db;

    public DashboardController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var officesCount = await _db.Offices.CountAsync(cancellationToken);
        var activeOfficesCount = await _db.Offices.CountAsync(o => o.Active, cancellationToken);
        var staffCount = await _db.Users.CountAsync(cancellationToken);
        var clientsCount = await _db.Clients.CountAsync(cancellationToken);
        var activeClientsCount = await _db.Clients.CountAsync(c => c.Status == ClientStatus.Active, cancellationToken);

        var outstandingLoansCount = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Disbursed, cancellationToken);

        // Distinct loans carrying at least one installment, due within the last year, whose
        // principal isn't fully paid off. Bounded to a year (not all-time) and keyed off actual
        // paid amounts rather than the `Paid` flag deliberately — on real imported data, `Paid`
        // and `TotalDue` were found to be unset on every single row regardless of true payment
        // status, so amounts paid vs. Principal is the only reliable signal, and without the
        // date bound this table (millions of rows on a real portfolio) makes an already
        // low-selectivity filter scan the entire history back to loans long since resolved.
        var lateLoanWindowStart = today.AddYears(-1);
        var lateLoansCount = await _db.LoanRepaymentSchedules
            .Where(s => s.DueDate < today && s.DueDate >= lateLoanWindowStart
                     && (s.Principal ?? 0) > (s.PrincipalPaid ?? 0)
                     && s.Loan!.Status == LoanStatus.Disbursed)
            .Select(s => s.LoanId)
            .Distinct()
            .CountAsync(cancellationToken);

        var disbursementsThisMonth = _db.LoanTransactions
            .Where(t => t.TransactionType == LoanTransactionType.Disbursement && !t.Reversed && t.Date >= monthStart && t.Date <= today);
        var disbursementsThisMonthCount = await disbursementsThisMonth.CountAsync(cancellationToken);
        var disbursementsThisMonthAmount = await disbursementsThisMonth.SumAsync(t => t.Amount ?? 0, cancellationToken);

        var repaymentsThisMonth = _db.LoanTransactions
            .Where(t => t.TransactionType == LoanTransactionType.Repayment && !t.Reversed && t.Date >= monthStart && t.Date <= today);
        var repaymentsThisMonthCount = await repaymentsThisMonth.CountAsync(cancellationToken);
        var repaymentsThisMonthAmount = await repaymentsThisMonth.SumAsync(t => t.Amount ?? 0, cancellationToken);

        var pendingLoanApplicationsCount = await _db.LoanApplications.CountAsync(a => a.Status == ApprovalStatus.Pending, cancellationToken);
        var pendingStaffOnboardingCount = await _db.Users.CountAsync(u => u.OnboardingStatus == UserOnboardingStatus.Pending, cancellationToken);

        return Ok(new DashboardSummaryResponse(
            officesCount,
            activeOfficesCount,
            staffCount,
            clientsCount,
            activeClientsCount,
            outstandingLoansCount,
            lateLoansCount,
            disbursementsThisMonthCount,
            disbursementsThisMonthAmount,
            repaymentsThisMonthCount,
            repaymentsThisMonthAmount,
            pendingLoanApplicationsCount,
            pendingStaffOnboardingCount));
    }
}
