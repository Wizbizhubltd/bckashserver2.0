using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>Group report, group breakdown, group indicator report (FR-RPT-1).</summary>
public interface IGroupReportService
{
    bool CanHandle(ScheduledReportName reportName);

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
