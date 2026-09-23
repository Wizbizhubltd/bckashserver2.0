using BCKash.Api.Contracts;
using BCKash.Application.GeneralLedger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

/// <summary>FR-GL-7 — basic/unstyled at this stage per the phase spec; polish is Phase 8's reporting pass.</summary>
[ApiController]
[Route("api/gl/reports")]
[Authorize]
public class GlReportsController : ControllerBase
{
    private readonly IGlReportService _reportService;

    public GlReportsController(IGlReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("trial-balance")]
    public async Task<ActionResult<IReadOnlyCollection<TrialBalanceRowResponse>>> TrialBalance(
        [FromQuery] int? officeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        var rows = await _reportService.GetTrialBalanceAsync(officeId, fromDate, toDate, cancellationToken);
        return Ok(rows.Select(r => new TrialBalanceRowResponse(r.GlAccountId, r.Name, r.GlCode, r.AccountType, r.TotalDebit, r.TotalCredit)).ToList());
    }

    [HttpGet("balance-sheet")]
    public async Task<ActionResult<IReadOnlyCollection<AccountTypeBalanceResponse>>> BalanceSheet(
        [FromQuery] int? officeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        var sections = await _reportService.GetBalanceSheetAsync(officeId, fromDate, toDate, cancellationToken);
        return Ok(sections.Select(ToResponse).ToList());
    }

    [HttpGet("profit-and-loss")]
    public async Task<ActionResult<ProfitAndLossResponse>> ProfitAndLoss(
        [FromQuery] int? officeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetProfitAndLossAsync(officeId, fromDate, toDate, cancellationToken);
        return Ok(new ProfitAndLossResponse(result.Sections.Select(ToResponse).ToList(), result.NetProfit));
    }

    [HttpGet("cash-flow")]
    public async Task<ActionResult<IReadOnlyCollection<CashFlowPeriodResponse>>> CashFlow(
        [FromQuery] int? officeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        var periods = await _reportService.GetCashFlowAsync(officeId, fromDate, toDate, cancellationToken);
        return Ok(periods.Select(p => new CashFlowPeriodResponse(p.Period, p.NetMovement)).ToList());
    }

    private static AccountTypeBalanceResponse ToResponse(AccountTypeBalance b) => new(b.AccountType, b.TotalDebit, b.TotalCredit, b.Net);
}
