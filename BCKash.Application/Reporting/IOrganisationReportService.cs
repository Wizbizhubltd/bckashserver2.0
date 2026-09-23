using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>Products summary and audit report (FR-RPT-1).</summary>
public interface IOrganisationReportService
{
    bool CanHandle(ScheduledReportName reportName);

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
