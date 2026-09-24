using BCKash.Api.Contracts;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/gl/office-transfers")]
[Authorize]
public class OfficeTransfersController : ControllerBase
{
    private const string ManagePolicy = "Permission:gl.manage";

    private readonly BCKashDbContext _db;
    private readonly IOfficeTransferService _transferService;

    public OfficeTransfersController(BCKashDbContext db, IOfficeTransferService transferService)
    {
        _db = db;
        _transferService = transferService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OfficeTransactionResponse>>> List(CancellationToken cancellationToken)
    {
        var transactions = await _db.OfficeTransactions.OrderByDescending(t => t.Date).ToListAsync(cancellationToken);
        return Ok(transactions.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(CreateOfficeTransferRequest request, CancellationToken cancellationToken)
    {
        var result = await _transferService.CreateAsync(
            request.FromOfficeId, request.ToOfficeId, request.CurrencyId, request.Amount, request.GlAccountId,
            request.Date, request.Notes, cancellationToken);

        return result.Outcome switch
        {
            OfficeTransferOutcome.Success => Ok(ToResponse(result.Transaction!)),
            OfficeTransferOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            OfficeTransferOutcome.SameOffice => Problem(title: "Source and destination offices must differ.", statusCode: StatusCodes.Status400BadRequest),
            OfficeTransferOutcome.AccountNotFound => Problem(title: "The selected GL account was not found.", statusCode: StatusCodes.Status400BadRequest),
            OfficeTransferOutcome.ClosurePeriod => Problem(title: "This date falls within a closed accounting period for one of the offices.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static OfficeTransactionResponse ToResponse(OfficeTransaction t) =>
        new(t.Id, t.FromOfficeId, t.ToOfficeId, t.CurrencyId, t.Amount, t.Date, t.Notes);
}
