using BCKash.Api.Contracts;
using BCKash.Application.Identity;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
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

    /// <summary>Recording a repayment (and seeing where to pay) — staff who only take repayments, or anyone who services loans.</summary>
    private const string RecordPolicy = "Permission:loan-repayments.record|loan-servicing.manage";

    private readonly BCKashDbContext _db;
    private readonly ILoanRepaymentService _repaymentService;
    private readonly IOfficeScope _scope;
    private readonly ICurrencyDisplayProvider _currency;

    public LoanRepaymentsController(BCKashDbContext db, ILoanRepaymentService repaymentService, IOfficeScope scope, ICurrencyDisplayProvider currency)
    {
        _db = db;
        _repaymentService = repaymentService;
        _scope = scope;
        _currency = currency;
    }

    /// <summary>Where the customer should pay: the loan's office default bank account (Office Funding in the control portal).</summary>
    [HttpGet("pay-into")]
    [Authorize(Policy = RecordPolicy)]
    public async Task<ActionResult<RepaymentAccountResponse>> PayInto(int loanId, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans.Where(l => l.Id == loanId).Select(l => new { l.OfficeId }).FirstOrDefaultAsync(cancellationToken);
        if (loan is null || !await _scope.CanAccessOfficeAsync(loan.OfficeId, cancellationToken))
        {
            return NotFound();
        }

        var officeName = loan.OfficeId is null ? null : await _db.Offices.Where(o => o.Id == loan.OfficeId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken);
        var account = await _db.OfficeBankAccounts
            .Where(a => a.OfficeId == loan.OfficeId && a.IsDefault && a.Active)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var remaining = await _repaymentService.RemainingAsync(loanId, cancellationToken) ?? 0;
        var waiting = await _db.RepaymentSubmissions.Where(s => s.LoanId == loanId && s.Status == RepaymentSubmissionStatus.Pending).SumAsync(s => s.Amount, cancellationToken);
        return Ok(new RepaymentAccountResponse(officeName, account?.BankName, account?.AccountName, account?.AccountNumber, Math.Max(0, remaining - waiting)));
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

    /// <summary>
    /// Records a repayment. From staff who don't confirm repayments themselves it waits for the office manager
    /// (202 with the pending submission); otherwise it's posted against the loan straight away (201).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = RecordPolicy)]
    public async Task<IActionResult> Record(
        int loanId, RecordRepaymentRequest request, [FromServices] ILoanNotificationService notifications, [FromServices] IRepaymentSubmissionService submissions, CancellationToken cancellationToken)
    {
        // Only for loans in the caller's own office(s) — out-of-scope loans stay invisible.
        var officeId = await _db.Loans.Where(l => l.Id == loanId).Select(l => new { l.OfficeId }).FirstOrDefaultAsync(cancellationToken);
        if (officeId is not null && !await _scope.CanAccessOfficeAsync(officeId.OfficeId, cancellationToken))
        {
            return NotFound();
        }

        if (await submissions.NeedsConfirmationAsync(cancellationToken))
        {
            var submitted = await submissions.SubmitAsync(loanId, request.Amount, request.PaymentTypeId, request.Date, request.Notes, cancellationToken);
            return submitted.Outcome switch
            {
                RepaymentSubmissionOutcome.Success => StatusCode(StatusCodes.Status202Accepted, (await ToSubmissionResponsesAsync([submitted.Submission!], submissions, cancellationToken)).Single()),
                RepaymentSubmissionOutcome.ExceedsBalance => await ExceedsBalanceProblemAsync(loanId, cancellationToken),
                _ => SubmissionProblem(submitted),
            };
        }

        var result = await _repaymentService.RecordRepaymentAsync(loanId, request.Amount, request.PaymentTypeId, request.Date, request.Notes, cancellationToken);
        if (result.Outcome == LoanRepaymentWriteOutcome.Success)
        {
            await notifications.PaymentReceivedAsync(loanId, request.Amount, request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        }

        return result.Outcome switch
        {
            LoanRepaymentWriteOutcome.Success => CreatedAtAction(nameof(List), new { loanId }, ToResponse(result.Transaction!)),
            LoanRepaymentWriteOutcome.NotFound => NotFound(),
            LoanRepaymentWriteOutcome.InvalidAmount => Problem(title: "Repayment amount must be greater than zero.", statusCode: StatusCodes.Status400BadRequest),
            LoanRepaymentWriteOutcome.InvalidLoanStatus => Problem(title: "This loan cannot receive repayments in its current status.", statusCode: StatusCodes.Status400BadRequest),
            LoanRepaymentWriteOutcome.ExceedsBalance => await ExceedsBalanceProblemAsync(loanId, cancellationToken),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Repayments recorded for this loan that wait for (or had) the office manager's confirmation, newest first.</summary>
    [HttpGet("submissions")]
    public async Task<ActionResult<IReadOnlyList<RepaymentSubmissionResponse>>> Submissions(int loanId, [FromServices] IRepaymentSubmissionService submissions, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans.Where(l => l.Id == loanId).Select(l => new { l.OfficeId }).FirstOrDefaultAsync(cancellationToken);
        if (loan is null || !await _scope.CanAccessOfficeAsync(loan.OfficeId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.RepaymentSubmissions
            .Where(s => s.LoanId == loanId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);
        return Ok(await ToSubmissionResponsesAsync(items, submissions, cancellationToken));
    }

    /// <summary>The office manager confirms the money arrived: the repayment is posted and the customer gets their receipt.</summary>
    [HttpPost("submissions/{submissionId:int}/approve")]
    public async Task<IActionResult> ApproveSubmission(int loanId, int submissionId, [FromServices] IRepaymentSubmissionService submissions, CancellationToken cancellationToken)
    {
        var result = await submissions.ApproveAsync(submissionId, cancellationToken);
        return result.Outcome == RepaymentSubmissionOutcome.Success && result.Submission!.LoanId == loanId
            ? Ok((await ToSubmissionResponsesAsync([result.Submission], submissions, cancellationToken)).Single())
            : SubmissionProblem(result);
    }

    /// <summary>The office manager says the money wasn't received as described — the repayment never counts.</summary>
    [HttpPost("submissions/{submissionId:int}/dispute")]
    public async Task<IActionResult> DisputeSubmission(int loanId, int submissionId, ReasonRequest request, [FromServices] IRepaymentSubmissionService submissions, CancellationToken cancellationToken)
    {
        var result = await submissions.DisputeAsync(submissionId, request.Reason, cancellationToken);
        return result.Outcome == RepaymentSubmissionOutcome.Success && result.Submission!.LoanId == loanId
            ? Ok((await ToSubmissionResponsesAsync([result.Submission], submissions, cancellationToken)).Single())
            : SubmissionProblem(result);
    }

    /// <summary>Says how much the loan can still take — nothing, once it's fully repaid.</summary>
    private async Task<ObjectResult> ExceedsBalanceProblemAsync(int loanId, CancellationToken cancellationToken)
    {
        var remaining = await _repaymentService.RemainingAsync(loanId, cancellationToken) ?? 0;
        var waiting = await _db.RepaymentSubmissions.Where(s => s.LoanId == loanId && s.Status == RepaymentSubmissionStatus.Pending).SumAsync(s => s.Amount, cancellationToken);
        var room = Math.Max(0, remaining - waiting);
        var currency = await _currency.GetAsync(cancellationToken);
        var title = remaining <= 0
            ? "This loan is completed — fully repaid, with nothing left to pay."
            : waiting > 0
                ? $"That's more than the client still owes. {currency.Format(remaining)} is left, {currency.Format(waiting)} of it already waiting for confirmation, so at most {currency.Format(room)} more can be recorded."
                : $"That's more than the client still owes. At most {currency.Format(remaining)} can be paid.";
        return Problem(title: title, statusCode: StatusCodes.Status400BadRequest);
    }

    private ObjectResult SubmissionProblem(RepaymentSubmissionResult result) => result.Outcome switch
    {
        RepaymentSubmissionOutcome.NotFound => Problem(title: "That repayment wasn't found.", statusCode: StatusCodes.Status404NotFound),
        RepaymentSubmissionOutcome.InvalidAmount => Problem(title: "Repayment amount must be greater than zero.", statusCode: StatusCodes.Status400BadRequest),
        RepaymentSubmissionOutcome.InvalidLoanStatus => Problem(title: "This loan cannot receive repayments in its current status.", statusCode: StatusCodes.Status400BadRequest),
        RepaymentSubmissionOutcome.AlreadyReviewed => Problem(title: "This repayment has already been confirmed or disputed.", statusCode: StatusCodes.Status409Conflict),
        RepaymentSubmissionOutcome.ReasonRequired => Problem(title: "Say why the repayment is disputed.", statusCode: StatusCodes.Status400BadRequest),
        RepaymentSubmissionOutcome.NotAllowed => Problem(title: "Only the office manager confirms repayments.", statusCode: StatusCodes.Status403Forbidden),
        RepaymentSubmissionOutcome.PostingFailed => Problem(
            title: result.RepaymentOutcome switch
            {
                LoanRepaymentWriteOutcome.InvalidLoanStatus => "The loan can no longer take repayments, so this one can't be confirmed. Dispute it instead.",
                LoanRepaymentWriteOutcome.ExceedsBalance => "This repayment is more than the client still owes, so it can't be confirmed. Dispute it instead.",
                _ => "The repayment couldn't be posted to the loan.",
            },
            statusCode: StatusCodes.Status409Conflict),
        // A submission for a different loan than the route's: treat it as not found.
        _ => Problem(title: "That repayment wasn't found.", statusCode: StatusCodes.Status404NotFound),
    };

    private async Task<List<RepaymentSubmissionResponse>> ToSubmissionResponsesAsync(
        IReadOnlyList<RepaymentSubmission> items, IRepaymentSubmissionService submissions, CancellationToken cancellationToken)
    {
        var userIds = items.SelectMany(s => new[] { s.SubmittedById, s.ReviewedById }).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } name ? name : u.Email, cancellationToken);

        var responses = new List<RepaymentSubmissionResponse>();
        foreach (var s in items)
        {
            responses.Add(new RepaymentSubmissionResponse(
                s.Id, s.LoanId, s.Amount, s.PaymentDate, s.Notes, s.Status,
                s.SubmittedById.HasValue ? names.GetValueOrDefault(s.SubmittedById.Value) : null, s.CreatedAt,
                s.ReviewedById.HasValue ? names.GetValueOrDefault(s.ReviewedById.Value) : null, s.ReviewedAt, s.DisputeReason,
                s.LoanTransactionId,
                s.Status == RepaymentSubmissionStatus.Pending && await submissions.CanReviewAsync(s.OfficeId, cancellationToken)));
        }

        return responses;
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
