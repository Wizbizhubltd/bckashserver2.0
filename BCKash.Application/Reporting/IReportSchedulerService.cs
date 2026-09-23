using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

public enum ReportScheduleWriteOutcome
{
    Success,
    NotFound,
}

public record ReportScheduleWriteResult(ReportScheduleWriteOutcome Outcome, ReportScheduler? Schedule = null);

/// <summary>
/// Scheduled report CRUD and execution (BR-RPT-2, FR-RPT-2) — runs the report via
/// <see cref="IReportCatalogService"/>, renders it via <see cref="IReportExporter"/> in the
/// schedule's configured format, and emails it to <c>EmailRecipients</c> (comma-separated, per
/// the legacy column's free-text shape).
/// </summary>
public interface IReportSchedulerService
{
    Task<ReportScheduleWriteResult> CreateAsync(ReportScheduler schedule, CancellationToken cancellationToken = default);

    Task<ReportScheduleWriteResult> UpdateAsync(int id, ReportScheduler updated, CancellationToken cancellationToken = default);

    Task<ReportScheduleWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Runs one schedule immediately, regardless of its NextRunDate.</summary>
    Task<ReportScheduleWriteResult> RunAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs every active schedule whose NextRunDate is due (&lt;= today), then advances it. No
    /// job scheduler exists anywhere in this codebase — this is an admin-triggered stand-in,
    /// same precedent as campaigns/payroll/expenses/savings-interest/NPA recompute.
    /// </summary>
    Task<int> GenerateDueRunsAsync(CancellationToken cancellationToken = default);
}
