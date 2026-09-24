using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>FR-LN-16 (record/allocate) and FR-LN-18 (reversal).</summary>
[ApiController]
[Route("api/v1/loans/{loanId:int}/repayments")]
[Authorize]
public class LoanRepaymentsController : ControllerBase
{
    private const string ServicingPolicy = "Permission:loan-servicing.manage";

    private readonly BCKashDbContext _db;
    private readonly ILoanRepaymentService _repaymentService;

    public LoanRepaymentsController(BCKashDbContext db, ILoanRepaymentService repaymentService)
    {
        _db = db;
        _repaymentService = repaymentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanTransactionResponse>>> List(int loanId, CancellationToken cancellationToken)
    {
        if (!await _db.Loans.AnyAsync(l => l.Id == loanId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.LoanTransactions
            .Where(t => t.LoanId == loanId)
            .OrderByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<LoanTransactionResponse>> Record(int loanId, RecordRepaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _repaymentService.RecordRepaymentAsync(loanId, request.Amount, request.PaymentTypeId, request.Date, request.Notes, cancellationToken);

        return result.Outcome switch
        {
            LoanRepaymentWriteOutcome.Success => CreatedAtAction(nameof(List), new { loanId }, ToResponse(result.Transaction!)),
            LoanRepaymentWriteOutcome.NotFound => NotFound(),
            LoanRepaymentWriteOutcome.InvalidAmount => Problem(title: "Repayment amount must be greater than zero.", statusCode: StatusCodes.Status400BadRequest),
            LoanRepaymentWriteOutcome.InvalidLoanStatus => Problem(title: "This loan cannot receive repayments in its current status.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{transactionId:int}/reverse")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Reverse(int loanId, int transactionId, CancellationToken cancellationToken)
    {
        var result = await _repaymentService.ReverseAsync(transactionId, cancellationToken);

        return result.Outcome switch
        {
            LoanRepaymentWriteOutcome.Success => Ok(ToResponse(result.Transaction!)),
            LoanRepaymentWriteOutcome.TransactionNotFound => NotFound(),
            LoanRepaymentWriteOutcome.AlreadyReversed => Problem(title: "This transaction was already reversed.", statusCode: StatusCodes.Status400BadRequest),
            LoanRepaymentWriteOutcome.NotReversible => Problem(title: "This transaction isn't reversible.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static LoanTransactionResponse ToResponse(LoanTransaction t) =>
        new(t.Id, t.LoanId, t.TransactionType, t.Amount, t.Principal, t.Interest, t.Fee, t.Penalty, t.Overpayment, t.Date, t.Reversible, t.Reversed, t.Notes);
}
