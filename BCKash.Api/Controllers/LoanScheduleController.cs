using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>Read-only view of a loan's repayment schedule (FR-LN-12 to FR-LN-14), generated at disbursement — see LoanService.DisburseAsync.</summary>
[ApiController]
[Route("api/v1/loans/{loanId:int}/schedule")]
[Authorize]
public class LoanScheduleController : ControllerBase
{
    private readonly BCKashDbContext _db;

    public LoanScheduleController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ScheduleInstallmentResponse>>> List(int loanId, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans.Where(l => l.Id == loanId).Select(l => new { l.SavingsRate }).FirstOrDefaultAsync(cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        var items = await _db.LoanRepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.Installment)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(s => ToResponse(s) with { CustomerPays = s.TotalDue is { } due ? ClientSavingsRules.GrossUp(due, loan.SavingsRate) : null }).ToList());
    }

    private static ScheduleInstallmentResponse ToResponse(LoanRepaymentSchedule s) => new(
        s.Id, s.Installment, s.DueDate,
        s.Principal, s.PrincipalPaid, s.PrincipalWaived, s.PrincipalWrittenOff,
        s.Interest, s.InterestPaid, s.InterestWaived, s.InterestWrittenOff,
        s.Fees, s.FeesPaid, s.Penalty, s.PenaltyPaid,
        s.TotalDue, s.Paid);
}
