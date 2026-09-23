using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>
/// Single entry point over the full FRD §14 catalog (29 reports) — dispatches to whichever
/// category service (<see cref="IClientReportService"/>, <see cref="ILoanReportService"/>,
/// <see cref="Application.GeneralLedger.IGlReportService"/>, <see cref="IGroupReportService"/>,
/// <see cref="ISavingsReportService"/>, <see cref="IOrganisationReportService"/>) actually
/// implements the requested report. Used by both the on-demand reports endpoint and
/// <see cref="IReportSchedulerService"/>, so scheduling and on-demand generation always run the
/// exact same code.
/// </summary>
public interface IReportCatalogService
{
    /// <summary>All 29 (name, category) pairs, for a discoverability endpoint.</summary>
    IReadOnlyList<(ScheduledReportName Name, ScheduledReportCategory Category)> ListCatalog();

    Task<ReportResult> RunAsync(ScheduledReportName reportName, ReportFilter filter, CancellationToken cancellationToken = default);
}
