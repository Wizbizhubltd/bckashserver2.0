using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>
/// Disbursed loans, loan portfolio, expected repayments, repayments, collection, arrears, loan
/// sizes, individual indicator, and loan officer performance reports (FR-RPT-1).
/// </summary>
public interface ILoanReportService
{
    bool CanHandle(ScheduledReportName reportName);

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
