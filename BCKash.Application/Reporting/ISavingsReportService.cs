using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>
/// Savings account, savings balance, savings transaction, and fixed term maturity reports
/// (FR-RPT-1). Fixed term maturity has no backing data — this schema doesn't model fixed-term/
/// maturity-dated savings products at all, only regular passbook-style accounts — so it always
/// returns an empty (but real, not stubbed) result; see docs/phase9-communications-reporting-spec.md.
/// </summary>
public interface ISavingsReportService
{
    bool CanHandle(ScheduledReportName reportName);

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
