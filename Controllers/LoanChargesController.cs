using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Loan-level charges (FR-LN-6/FR-ORG-5) — ownership-scoped CRUD, no service, same shape as
/// Phase 4's guarantor/collateral sub-resource controllers. <see cref="SaveLoanChargeRequest.Amount"/>
/// is entered directly rather than computed from the charge's percentage/installment-basis
/// calculation option — automatic calculation needs the repayment schedule, which is blocked on
/// FR-LN-15 (see LoanChargeResponse's doc comment).
/// </summary>
[ApiController]
[Route("api/loans/{loanId:int}/charges")]
[Authorize]
public class LoanChargesController : ControllerBase
{
    private const string ServicingPolicy = "Permission:loan-servicing.manage";

    private readonly BCKashDbContext _db;

    public LoanChargesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanChargeResponse>>> List(int loanId, CancellationToken cancellationToken)
    {
        if (!await _db.Loans.AnyAsync(l => l.Id == loanId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.LoanCharges.Where(c => c.LoanId == loanId).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanChargeResponse>> Get(int loanId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.LoanCharges.FirstOrDefaultAsync(c => c.Id == id && c.LoanId == loanId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<LoanChargeResponse>> Create(int loanId, SaveLoanChargeRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Loans.AnyAsync(l => l.Id == loanId, cancellationToken))
        {
            return NotFound();
        }

        if (request.ChargeId.HasValue)
        {
            var referencedCharge = await _db.Charges.FindAsync([request.ChargeId.Value], cancellationToken);
            if (referencedCharge is null || referencedCharge.Product != ChargeProduct.Loan)
            {
                return Problem(title: "ChargeId must reference an existing charge scoped to Loan.", statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var item = new LoanCharge
        {
            LoanId = loanId,
            ChargeId = request.ChargeId,
            Penalty = request.Penalty,
            ChargeType = request.ChargeType,
            ChargeOption = request.ChargeOption,
            Amount = request.Amount,
            DueDate = request.DueDate,
            GracePeriod = request.GracePeriod,
        };
        _db.LoanCharges.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { loanId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Update(int loanId, int id, SaveLoanChargeRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.LoanCharges.FirstOrDefaultAsync(c => c.Id == id && c.LoanId == loanId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (request.ChargeId.HasValue)
        {
            var referencedCharge = await _db.Charges.FindAsync([request.ChargeId.Value], cancellationToken);
            if (referencedCharge is null || referencedCharge.Product != ChargeProduct.Loan)
            {
                return Problem(title: "ChargeId must reference an existing charge scoped to Loan.", statusCode: StatusCodes.Status400BadRequest);
            }
        }

        item.ChargeId = request.ChargeId;
        item.Penalty = request.Penalty;
        item.ChargeType = request.ChargeType;
        item.ChargeOption = request.ChargeOption;
        item.Amount = request.Amount;
        item.DueDate = request.DueDate;
        item.GracePeriod = request.GracePeriod;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Delete(int loanId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.LoanCharges.FirstOrDefaultAsync(c => c.Id == id && c.LoanId == loanId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.LoanCharges.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static LoanChargeResponse ToResponse(LoanCharge c) =>
        new(c.Id, c.LoanId, c.ChargeId, c.Penalty, c.Waived, c.ChargeType, c.ChargeOption, c.Amount, c.AmountPaid, c.DueDate, c.GracePeriod);
}
