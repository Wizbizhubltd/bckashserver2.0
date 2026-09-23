using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>
/// The standard filter set every report supports at minimum (FR-RPT-1). Not every report uses
/// every field — a report ignores whichever of these don't apply to it (e.g. a client-only
/// report ignores LoanStatus/LoanProductId).
/// </summary>
public record ReportFilter(
    int? OfficeId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? LoanOfficerId = null,
    LoanStatus? LoanStatus = null,
    int? LoanProductId = null);

/// <summary>
/// One generic tabular report result — every report in the FRD §14 catalog (29 total, no
/// per-report column spec exists anywhere in the FRD/BRD) returns this same shape, so a single
/// PDF/CSV/XLS exporter (<see cref="IReportExporter"/>) and a single scheduler
/// (<see cref="IReportSchedulerService"/>) can handle all of them uniformly, rather than 29
/// bespoke response types and 29 bespoke export code paths. See
/// docs/phase9-communications-reporting-spec.md for the column set chosen for each report.
/// </summary>
public record ReportResult(ScheduledReportName ReportName, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);
