using BCKash.Application.Reporting;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// Group report, group breakdown, group indicator report (FR-RPT-1). Column sets are a
/// documented modeling choice — see docs/phase9-communications-reporting-spec.md.
/// </summary>
public class GroupReportService : IGroupReportService
{
    private static readonly ScheduledReportName[] Handled =
        [ScheduledReportName.GroupReport, ScheduledReportName.GroupBreakdown, ScheduledReportName.GroupIndicatorReport];

    private readonly BCKashDbContext _db;

    public GroupReportService(BCKashDbContext db)
    {
        _db = db;
    }

    public bool CanHandle(ScheduledReportName reportName) => Handled.Contains(reportName);

    public Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default) => reportName switch
    {
        ScheduledReportName.GroupReport => GroupOverviewAsync(filter, cancellationToken),
        ScheduledReportName.GroupBreakdown => GroupBreakdownAsync(filter, cancellationToken),
        ScheduledReportName.GroupIndicatorReport => GroupIndicatorAsync(filter, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(reportName)),
    };

    private IQueryable<Group> FilteredGroups(ReportFilter filter)
    {
        var query = _db.Groups.Include(g => g.Office).AsQueryable();
        if (filter.OfficeId.HasValue)
        {
            query = query.Where(g => g.OfficeId == filter.OfficeId);
        }

        return query;
    }

    private async Task<ReportResult> GroupOverviewAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var groups = await FilteredGroups(filter).OrderBy(g => g.Id).ToListAsync(cancellationToken);
        var memberCounts = await _db.GroupClients.Where(gc => gc.RemovedAt == null).GroupBy(gc => gc.GroupId).Select(g => new { GroupId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.GroupId ?? 0, x => x.Count, cancellationToken);

        var rows = groups.Select(g => (IReadOnlyList<string?>)
            [g.AccountNo, g.Name, g.Office?.Name, g.Status.ToString(), memberCounts.GetValueOrDefault(g.Id).ToString(), g.JoinedDate?.ToString("yyyy-MM-dd")]).ToList();

        return new ReportResult(ScheduledReportName.GroupReport, ["Account No", "Name", "Office", "Status", "Member Count", "Joined Date"], rows);
    }

    /// <summary>Members within each group, one row per member.</summary>
    private async Task<ReportResult> GroupBreakdownAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var groupIds = await FilteredGroups(filter).Select(g => g.Id).ToListAsync(cancellationToken);

        var members = await _db.GroupClients
            .Where(gc => groupIds.Contains(gc.GroupId ?? 0) && gc.RemovedAt == null)
            .Select(gc => new
            {
                GroupName = _db.Groups.Where(g => g.Id == gc.GroupId).Select(g => g.Name).FirstOrDefault(),
                ClientName = _db.Clients.Where(c => c.Id == gc.ClientId).Select(c => c.FullName ?? c.DisplayName).FirstOrDefault(),
                ClientAccountNo = _db.Clients.Where(c => c.Id == gc.ClientId).Select(c => c.AccountNo).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var rows = members.Select(m => (IReadOnlyList<string?>)[m.GroupName, m.ClientAccountNo, m.ClientName]).ToList();

        return new ReportResult(ScheduledReportName.GroupBreakdown, ["Group", "Client Account No", "Client Name"], rows);
    }

    /// <summary>Per group: member count, active loans across members, and total outstanding.</summary>
    private async Task<ReportResult> GroupIndicatorAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var groups = await FilteredGroups(filter).ToListAsync(cancellationToken);

        var rows = new List<IReadOnlyList<string?>>();
        foreach (var group in groups)
        {
            var memberIds = await _db.GroupClients.Where(gc => gc.GroupId == group.Id && gc.RemovedAt == null).Select(gc => gc.ClientId).ToListAsync(cancellationToken);
            var groupLoans = _db.Loans.Where(l => l.GroupId == group.Id || (l.ClientId.HasValue && memberIds.Contains(l.ClientId)));
            var activeLoans = await groupLoans.CountAsync(l => l.Status == LoanStatus.Disbursed, cancellationToken);
            var outstanding = await _db.LoanRepaymentSchedules
                .Where(s => groupLoans.Select(l => l.Id).Contains(s.LoanId ?? 0))
                .SumAsync(s => (s.Principal ?? 0m) - (s.PrincipalPaid ?? 0m), cancellationToken);

            rows.Add([group.Name, memberIds.Count.ToString(), activeLoans.ToString(), outstanding.ToString("F2")]);
        }

        return new ReportResult(ScheduledReportName.GroupIndicatorReport, ["Group", "Members", "Active Loans", "Outstanding Principal"], rows);
    }
}
