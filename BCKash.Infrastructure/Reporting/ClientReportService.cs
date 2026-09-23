using BCKash.Application.Reporting;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Client numbers report, clients overview, top clients report (FR-RPT-1). Column sets are a
/// documented modeling choice (no per-report spec exists in the FRD) — see
/// docs/phase9-communications-reporting-spec.md.
/// </summary>
public class ClientReportService : IClientReportService
{
    private static readonly ScheduledReportName[] Handled =
    [
        ScheduledReportName.ClientNumbersReport, ScheduledReportName.ClientsOverview, ScheduledReportName.TopClientsReport,
    ];

    private readonly BCKashDbContext _db;

    public ClientReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public bool CanHandle(ScheduledReportName reportName) => Handled.Contains(reportName);

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default) => reportName switch
    {
        ScheduledReportName.ClientNumbersReport => ClientNumbersAsync(filter, cancellationToken),
        ScheduledReportName.ClientsOverview => ClientsOverviewAsync(filter, cancellationToken),
        ScheduledReportName.TopClientsReport => TopClientsAsync(filter, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
    };

    private IQueryable<Client> FilteredClients(ReportFilter filter)
    {
        var query = _db.Clients.Include(c => c.Office).AsQueryable();

        if (filter.OfficeId.HasValue)
        {
            query = query.Where(c => c.OfficeId == filter.OfficeId);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(c => c.JoinedDate >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(c => c.JoinedDate <= filter.ToDate);
        }

        return query;
    }

    /// <summary>Client counts by status, grouped by office.</summary>
    private async Task<ReportResult> ClientNumbersAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var grouped = await FilteredClients(filter)
            .GroupBy(c => new { c.OfficeId, OfficeName = c.Office != null ? c.Office.Name : null })
            .Select(g => new
            {
                g.Key.OfficeName,
                Total = g.Count(),
                Active = g.Count(c => c.Status == ClientStatus.Active),
                Pending = g.Count(c => c.Status == ClientStatus.Pending),
                Inactive = g.Count(c => c.Status == ClientStatus.Inactive),
                Closed = g.Count(c => c.Status == ClientStatus.Closed),
            })
            .ToListAsync(cancellationToken);

        var rows = grouped.Select(g => (IReadOnlyList<string?>)
            [g.OfficeName, g.Total.ToString(), g.Active.ToString(), g.Pending.ToString(), g.Inactive.ToString(), g.Closed.ToString()]).ToList();

        return new ReportResult(ScheduledReportName.ClientNumbersReport, ["Office", "Total", "Active", "Pending", "Inactive", "Closed"], rows);
    }

    private async Task<ReportResult> ClientsOverviewAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var clients = await FilteredClients(filter).OrderBy(c => c.Id).ToListAsync(cancellationToken);

        var rows = clients.Select(c => (IReadOnlyList<string?>)
        [
            c.AccountNo, c.FullName ?? c.DisplayName, c.Office?.Name, c.Status.ToString(),
            c.Mobile, c.JoinedDate?.ToString("yyyy-MM-dd"),
        ]).ToList();

        return new ReportResult(ScheduledReportName.ClientsOverview, ["Account No", "Name", "Office", "Status", "Mobile", "Joined Date"], rows);
    }

    /// <summary>Ranked by total approved amount across the client's disbursed loans.</summary>
    private async Task<ReportResult> TopClientsAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var loans = _db.Loans.Where(l => l.Status == LoanStatus.Disbursed).AsQueryable();

        if (filter.OfficeId.HasValue)
        {
            loans = loans.Where(l => l.OfficeId == filter.OfficeId);
        }

        if (filter.FromDate.HasValue)
        {
            loans = loans.Where(l => l.DisbursementDate >= filter.FromDate);
        }

        if (filter.ToDate.HasValue)
        {
            loans = loans.Where(l => l.DisbursementDate <= filter.ToDate);
        }

        // Ordered in memory — SQLite (used in tests) can't translate ORDER BY over a decimal
        // aggregate server-side.
        var totals = (await loans
            .GroupBy(l => l.ClientId)
            .Select(g => new { ClientId = g.Key, Total = g.Sum(l => l.ApprovedAmount ?? 0m), LoanCount = g.Count() })
            .ToListAsync(cancellationToken))
            .OrderByDescending(g => g.Total)
            .Take(20)
            .ToList();

        var clientIds = totals.Where(t => t.ClientId.HasValue).Select(t => t.ClientId!.Value).ToList();
        var clients = await _db.Clients.Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var rows = totals.Select(t =>
        {
            clients.TryGetValue(t.ClientId ?? 0, out var client);
            return (IReadOnlyList<string?>)[client?.AccountNo, client?.FullName ?? client?.DisplayName, t.LoanCount.ToString(), t.Total.ToString("F2")];
        }).ToList();

        return new ReportResult(ScheduledReportName.TopClientsReport, ["Account No", "Name", "Loan Count", "Total Disbursed"], rows);
    }
}
