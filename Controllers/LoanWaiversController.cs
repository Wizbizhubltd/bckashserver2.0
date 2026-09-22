using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>FR-LN-20: waive interest or a specific charge component on one of the loan's schedule lines.</summary>
[ApiController]
[Route("api/loans/{loanId:int}/waivers")]
[Authorize]
public class LoanWaiversController : ControllerBase
{
    private const string ServicingPolicy = "Permission:loan-servicing.manage";

    private readonly BCKashDbContext _db;
    private readonly ILoanWaiverService _waiverService;

    public LoanWaiversController(BCKashDbContext db, ILoanWaiverService waiverService)
    {
        _db = db;
        _waiverService = waiverService;
    }

    [HttpPost]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<ScheduleInstallmentResponse>> Waive(int loanId, WaiveChargeRequest request, CancellationToken cancellationToken)
    {
        var scheduleBelongsToLoan = await _db.LoanRepaymentSchedules.AnyAsync(s => s.Id == request.ScheduleId && s.LoanId == loanId, cancellationToken);
        if (!scheduleBelongsToLoan)
        {
            return NotFound();
        }

        var result = await _waiverService.WaiveAsync(request.ScheduleId, request.Component, request.Amount, request.Reason, cancellationToken);

        return result.Outcome switch
        {
            LoanWaiverOutcome.Success => Ok(ToResponse(result.Schedule!)),
            LoanWaiverOutcome.ScheduleNotFound => NotFound(),
            LoanWaiverOutcome.InvalidAmount => Problem(title: "Waiver amount must be greater than zero.", statusCode: StatusCodes.Status400BadRequest),
            LoanWaiverOutcome.ReasonRequired => Problem(title: "A reason is required to waive a charge.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static ScheduleInstallmentResponse ToResponse(BCKash.Domain.Loans.LoanRepaymentSchedule s) => new(
        s.Id, s.Installment, s.DueDate,
        s.Principal, s.PrincipalPaid, s.PrincipalWaived, s.PrincipalWrittenOff,
        s.Interest, s.InterestPaid, s.InterestWaived, s.InterestWrittenOff,
        s.Fees, s.FeesPaid, s.Penalty, s.PenaltyPaid,
        s.TotalDue, s.Paid);
}
