using BCKash.Api.Contracts;
using BCKash.Application.Reporting;
using BCKash.Domain.Loans;
using BCKash.Domain.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

/// <summary>
/// Single dispatch surface over the full FRD §14 catalog (29 reports) — see
/// IReportCatalogService's doc comment for why one endpoint handles all of them instead of 29
/// bespoke routes.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = ViewPolicy)]
public class ReportsController : ControllerBase
{
    private const string ViewPolicy = "Permission:reports.view";

    private readonly IReportCatalogService _catalog;
    private readonly IReportExporter _exporter;

    public ReportsController(IReportCatalogService catalog, IReportExporter exporter)
    {
        _catalog = catalog;
        _exporter = exporter;
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<ReportCatalogEntryResponse>> Catalog() =>
        Ok(_catalog.ListCatalog().Select(e => new ReportCatalogEntryResponse(e.Name, e.Category)).ToList());

    /// <summary>FR-RPT-1's standard filter set. Add <c>?format=pdf|csv|xls</c> to download instead of receiving JSON.</summary>
    [HttpGet("{reportName}")]
    public async Task<IActionResult> Run(
        ScheduledReportName reportName,
        [FromQuery] int? officeId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? loanOfficerId,
        [FromQuery] LoanStatus? loanStatus,
        [FromQuery] int? loanProductId,
        [FromQuery] ReportSchedulerFileFormat? format,
        CancellationToken cancellationToken)
    {
        var filter = new ReportFilter(officeId, fromDate, toDate, loanOfficerId, loanStatus, loanProductId);
        var result = await _catalog.RunAsync(reportName, filter, cancellationToken);

        if (format is null)
        {
            return Ok(new ReportResultResponse(result.ReportName, result.Columns, result.Rows));
        }

        var bytes = _exporter.Export(result, format.Value);
        return File(bytes, _exporter.ContentType(format.Value), $"{reportName}.{_exporter.FileExtension(format.Value)}");
    }
}
