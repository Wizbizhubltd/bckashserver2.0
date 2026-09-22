using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>FR-LN-23's reschedule request workflow.</summary>
[ApiController]
[Route("api/loans/{loanId:int}/reschedule-requests")]
[Authorize]
public class LoanRescheduleController : ControllerBase
{
    private const string ServicingPolicy = "Permission:loan-servicing.manage";

    private readonly BCKashDbContext _db;
    private readonly ILoanRescheduleService _rescheduleService;

    public LoanRescheduleController(BCKashDbContext db, ILoanRescheduleService rescheduleService)
    {
        _db = db;
        _rescheduleService = rescheduleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanRescheduleRequestResponse>>> List(int loanId, CancellationToken cancellationToken)
    {
        if (!await _db.Loans.AnyAsync(l => l.Id == loanId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.LoanRescheduleRequests
            .Where(r => r.LoanId == loanId)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<LoanRescheduleRequestResponse>> Create(int loanId, CreateRescheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await _rescheduleService.RequestAsync(loanId, request.Principal, request.RescheduleFromDate, request.RecalculateInterest, request.Notes, cancellationToken);

        return result.Outcome switch
        {
            LoanRescheduleOutcome.Success => CreatedAtAction(nameof(List), new { loanId }, ToResponse(result.Request!)),
            LoanRescheduleOutcome.LoanNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Approve(int loanId, int id, CancellationToken cancellationToken)
    {
        var result = await _rescheduleService.ApproveAsync(id, cancellationToken);
        return ToResult(result);
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Reject(int loanId, int id, ReasonRequest? request, CancellationToken cancellationToken)
    {
        var result = await _rescheduleService.RejectAsync(id, request?.Reason, cancellationToken);
        return ToResult(result);
    }

    private IActionResult ToResult(LoanRescheduleResult result) => result.Outcome switch
    {
        LoanRescheduleOutcome.Success => Ok(ToResponse(result.Request!)),
        LoanRescheduleOutcome.NotFound => NotFound(),
        LoanRescheduleOutcome.LoanNotFound => NotFound(),
        LoanRescheduleOutcome.InvalidTransition => Problem(
            title: "This reschedule request can't be actioned from its current status, or the loan has no active schedule.",
            statusCode: StatusCodes.Status400BadRequest),
        LoanRescheduleOutcome.NothingToReschedule => Problem(
            title: "No schedule lines fall on or after the reschedule-from date.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static LoanRescheduleRequestResponse ToResponse(LoanRescheduleRequest r) => new(
        r.Id, r.LoanId, r.Principal, r.Status, r.RescheduleFromDate, r.RecalculateInterest != 0, r.Notes, r.ApprovedDate, r.RejectedDate);
}
