using BCKash.Api.Contracts;
using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/savings-accounts/{accountId:int}/charges")]
[Authorize]
public class SavingsChargesController : ControllerBase
{
    private const string ManagePolicy = "Permission:savings-accounts.manage";

    private readonly BCKashDbContext _db;
    private readonly ISavingsChargeService _chargeService;

    public SavingsChargesController(BCKashDbContext db, ISavingsChargeService chargeService)
    {
        _db = db;
        _chargeService = chargeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavingsChargeResponse>>> List(int accountId, CancellationToken cancellationToken)
    {
        var charges = await _db.SavingsCharges.Where(c => c.SavingsId == accountId).ToListAsync(cancellationToken);
        return Ok(charges.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SavingsChargeResponse>> Attach(int accountId, AttachSavingsChargeRequest request, CancellationToken cancellationToken)
    {
        var result = await _chargeService.AttachAsync(accountId, request.ChargeType, request.Penalty, request.Amount, request.DueDate, cancellationToken);
        return result.Outcome switch
        {
            SavingsChargeWriteOutcome.Success => CreatedAtAction(nameof(List), new { accountId }, ToResponse(result.Charge!)),
            SavingsChargeWriteOutcome.NotFound => NotFound(),
            SavingsChargeWriteOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{chargeId:int}/waive")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Waive(int accountId, int chargeId, CancellationToken cancellationToken)
    {
        var result = await _chargeService.WaiveAsync(chargeId, cancellationToken);
        return result.Outcome switch
        {
            SavingsChargeWriteOutcome.Success => Ok(ToResponse(result.Charge!)),
            SavingsChargeWriteOutcome.ChargeNotFound => NotFound(),
            SavingsChargeWriteOutcome.AlreadyWaived => Problem(title: "This charge has already been waived.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{chargeId:int}/pay")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Pay(int accountId, int chargeId, [FromBody] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await _chargeService.PayDueAsync(chargeId, date, cancellationToken);
        return result.Outcome switch
        {
            SavingsChargeWriteOutcome.Success => Ok(ToResponse(result.Charge!)),
            SavingsChargeWriteOutcome.ChargeNotFound => NotFound(),
            SavingsChargeWriteOutcome.NotFound => NotFound(),
            SavingsChargeWriteOutcome.AlreadyWaived => Problem(title: "This charge has been waived.", statusCode: StatusCodes.Status409Conflict),
            SavingsChargeWriteOutcome.AlreadyPaid => Problem(title: "This charge has already been paid.", statusCode: StatusCodes.Status409Conflict),
            SavingsChargeWriteOutcome.InsufficientBalance => Problem(
                title: "This charge would take the balance below the account's minimum balance (or overdraft limit).",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static SavingsChargeResponse ToResponse(SavingsCharge c) => new(c.Id, c.SavingsId, c.ChargeType, c.Penalty, c.Waived, c.Amount, c.AmountPaid, c.DueDate);
}
