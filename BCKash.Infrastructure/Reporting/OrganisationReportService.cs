using BCKash.Application.Reporting;
using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Products summary and audit report (FR-RPT-1). Column sets are a documented modeling choice —
/// see docs/phase9-communications-reporting-spec.md.
/// </summary>
public class OrganisationReportService : IOrganisationReportService
{
    private static readonly ScheduledReportName[] Handled = [ScheduledReportName.ProductsSummary, ScheduledReportName.AuditReport];

    private readonly BCKashDbContext _db;

    public OrganisationReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public bool CanHandle(ScheduledReportName reportName) => Handled.Contains(reportName);

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default) => reportName switch
    {
        ScheduledReportName.ProductsSummary => ProductsSummaryAsync(cancellationToken),
        ScheduledReportName.AuditReport => AuditReportAsync(filter, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
    };

    private async Task<ReportResult> ProductsSummaryAsync(CancellationToken cancellationToken)
    {
        var loanProducts = await _db.LoanProducts
            .Select(p => new
            {
                p.Name,
                AccountCount = _db.Loans.Count(l => l.LoanProductId == p.Id && l.Status == LoanStatus.Disbursed),
                Total = _db.Loans.Where(l => l.LoanProductId == p.Id && l.Status == LoanStatus.Disbursed).Sum(l => l.ApprovedAmount ?? 0m),
            })
            .ToListAsync(cancellationToken);

        var savingsProducts = await _db.SavingsProducts
            .Select(p => new
            {
                p.Name,
                AccountCount = _db.Savings.Count(s => s.SavingsProductId == p.Id),
                Total = _db.Savings.Where(s => s.SavingsProductId == p.Id).Sum(s => s.Balance ?? 0m),
            })
            .ToListAsync(cancellationToken);

        var rows = new List<IReadOnlyList<string?>>();
        rows.AddRange(loanProducts.Select(p => (IReadOnlyList<string?>)["Loan", p.Name, p.AccountCount.ToString(), p.Total.ToString("F2")]));
        rows.AddRange(savingsProducts.Select(p => (IReadOnlyList<string?>)["Savings", p.Name, p.AccountCount.ToString(), p.Total.ToString("F2")]));

        return new ReportResult(ScheduledReportName.ProductsSummary, ["Type", "Product", "Account Count", "Total"], rows);
    }

    private async Task<ReportResult> AuditReportAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = _db.AuditTrail.AsQueryable();

        if (filter.OfficeId.HasValue)
        {
            query = query.Where(a => a.OfficeId == filter.OfficeId);
        }

        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.CreatedAt >= from);
        }

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(a => a.CreatedAt <= to);
        }

        var entries = await query
            .Select(a => new
            {
                a.CreatedAt,
                UserName = _db.Users.Where(u => u.Id == a.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                a.Module,
                a.Action,
                a.Notes,
            })
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var rows = entries.Select(e => (IReadOnlyList<string?>)
            [e.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss"), e.UserName, e.Module, e.Action, e.Notes]).ToList();

        return new ReportResult(ScheduledReportName.AuditReport, ["Date", "User", "Module", "Action", "Notes"], rows);
    }
}
