using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>Client numbers report, clients overview, top clients report (FR-RPT-1).</summary>
public interface IClientReportService
{
    bool CanHandle(ScheduledReportName reportName);

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
