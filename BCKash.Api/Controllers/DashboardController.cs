using BCKash.Api.Contracts;
using BCKash.Application.Loans;
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
    // Loans that got past approval, whatever happened to them afterwards. Loans created here start as
    // Pending *after* their application is approved (awaiting disbursement), so those are caught by
    // ApprovedDate instead — a status of Pending alone only means "awaiting approval" on legacy loans.
    private static readonly LoanStatus[] ApprovedStatuses =
    [
        LoanStatus.Approved,
        LoanStatus.Disbursed,
        LoanStatus.PendingReschedule,
        LoanStatus.Rescheduled,
        LoanStatus.Closed,
        LoanStatus.Paid,
        LoanStatus.WrittenOff,
    ];

    private static readonly LoanStatus[] RejectedStatuses = [LoanStatus.Declined, LoanStatus.Rejected];

    // Legacy loans awaiting approval — only when no approval was ever recorded on them.
    private static readonly LoanStatus[] PendingApprovalStatuses = [LoanStatus.New, LoanStatus.Pending, LoanStatus.NeedChanges];

    private readonly BCKashDbContext _db;
    private readonly IOverdueRulesProvider _overdueRules;

    public DashboardController(BCKashDbContext db, IOverdueRulesProvider overdueRules)
    {
        _db = db;
        _overdueRules = overdueRules;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Settings → Loan → "Overdue & penalty rules": an instalment is late only once it's this many
        // days past due, and a loan defaulted only this many days after its final repayment date.
        var rules = await _overdueRules.GetAsync(cancellationToken);
        var repaymentOverdueBefore = today.AddDays(-rules.RepaymentOverdueDays);
        var loanOverdueBefore = today.AddDays(-rules.LoanOverdueDays);

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
            .Where(s => s.DueDate < repaymentOverdueBefore && s.DueDate >= lateLoanWindowStart
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

        var loanPortfolio = await LoanPortfolioAsync(repaymentOverdueBefore, loanOverdueBefore, cancellationToken);

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
            pendingStaffOnboardingCount,
            loanPortfolio));
    }

    /// <summary>
    /// Requests start as loan applications; approving one creates the loan. So pending and declined
    /// applications are counted alongside loans (approved ones already are — they became loans), and
    /// requested is what the client asked for across both, falling back to principal on imported loans
    /// that never recorded it. Approved uses the approved amount with the same fallback. Repaid is
    /// every non-reversed repayment transaction.
    /// </summary>
    private async Task<LoanPortfolioSummary> LoanPortfolioAsync(DateOnly repaymentOverdueBefore, DateOnly loanOverdueBefore, CancellationToken cancellationToken)
    {
        var loanGroups = await _db.Loans
            .GroupBy(l => new { l.Status, HasApproval = l.ApprovedDate != null })
            .Select(g => new
            {
                g.Key.Status,
                g.Key.HasApproval,
                Count = g.Count(),
                Requested = g.Sum(l => l.AppliedAmount ?? l.Principal ?? 0),
                Approved = g.Sum(l => l.ApprovedAmount ?? l.Principal ?? 0),
            })
            .ToListAsync(cancellationToken);

        var applicationGroups = await _db.LoanApplications
            .Where(a => a.Status != ApprovalStatus.Approved)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(a => a.Amount) })
            .ToListAsync(cancellationToken);
        var pendingApplications = applicationGroups.Where(a => a.Status == ApprovalStatus.Pending).ToList();
        var declinedApplications = applicationGroups.Where(a => a.Status == ApprovalStatus.Declined).ToList();

        var approved = loanGroups.Where(g => (g.HasApproval && !RejectedStatuses.Contains(g.Status)) || ApprovedStatuses.Contains(g.Status)).ToList();
        var rejected = loanGroups.Where(g => RejectedStatuses.Contains(g.Status)).ToList();
        var pendingLoans = loanGroups.Where(g => !g.HasApproval && PendingApprovalStatuses.Contains(g.Status)).ToList();

        var repayments = _db.LoanTransactions.Where(t => t.TransactionType == LoanTransactionType.Repayment && !t.Reversed);
        var repaidCount = await repayments.CountAsync(cancellationToken);
        var repaidAmount = await repayments.SumAsync(t => t.Amount ?? 0, cancellationToken);

        // Late and defaulted split disbursed loans with unpaid principal by their final repayment date,
        // so a loan is in one or the other, never both. The final date falls back to the last
        // instalment's due date, since loans created by this API (and some imported ones) never record
        // ExpectedMaturityDate. Paid amounts, not the `Paid` flag, decide what's unpaid — see the
        // late-loans note in Summary.

        // Late: a repayment date was missed, but the loan hasn't reached its final repayment date yet.
        var lateLoans = _db.Loans
            .Where(l => l.Status == LoanStatus.Disbursed
                     && (l.ExpectedMaturityDate ?? l.LoanRepaymentSchedules.Max(s => s.DueDate)) >= loanOverdueBefore
                     && l.LoanRepaymentSchedules.Any(s => s.DueDate < repaymentOverdueBefore && (s.Principal ?? 0) > (s.PrincipalPaid ?? 0)))
            .Select(l => l.Id);
        var lateCount = await lateLoans.CountAsync(cancellationToken);
        var lateAmount = await _db.LoanRepaymentSchedules
            .Where(s => lateLoans.Contains(s.LoanId!.Value) && s.DueDate < repaymentOverdueBefore && (s.Principal ?? 0) > (s.PrincipalPaid ?? 0))
            .SumAsync(s => (s.Principal ?? 0) - (s.PrincipalPaid ?? 0) + (s.Interest ?? 0) - (s.InterestPaid ?? 0), cancellationToken);

        // Defaulted: past the final repayment date with principal still unpaid.
        var defaultedLoans = _db.Loans
            .Where(l => l.Status == LoanStatus.Disbursed
                     && (l.ExpectedMaturityDate ?? l.LoanRepaymentSchedules.Max(s => s.DueDate)) < loanOverdueBefore
                     && l.LoanRepaymentSchedules.Any(s => (s.Principal ?? 0) > (s.PrincipalPaid ?? 0)))
            .Select(l => l.Id);
        var defaultedCount = await defaultedLoans.CountAsync(cancellationToken);
        var defaultedAmount = await _db.LoanRepaymentSchedules
            .Where(s => defaultedLoans.Contains(s.LoanId!.Value))
            .SumAsync(s => (s.Principal ?? 0) - (s.PrincipalPaid ?? 0) + (s.Interest ?? 0) - (s.InterestPaid ?? 0), cancellationToken);

        return new LoanPortfolioSummary(
            loanGroups.Sum(g => g.Requested) + applicationGroups.Sum(a => a.Amount),
            loanGroups.Sum(g => g.Count) + applicationGroups.Sum(a => a.Count),
            approved.Sum(g => g.Approved), approved.Sum(g => g.Count),
            rejected.Sum(g => g.Requested) + declinedApplications.Sum(a => a.Amount),
            rejected.Sum(g => g.Count) + declinedApplications.Sum(a => a.Count),
            pendingLoans.Sum(g => g.Requested) + pendingApplications.Sum(a => a.Amount),
            pendingLoans.Sum(g => g.Count) + pendingApplications.Sum(a => a.Count),
            repaidAmount, repaidCount,
            lateAmount, lateCount,
            defaultedAmount, defaultedCount);
    }
}
