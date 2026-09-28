using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/loan-applications")]
[Authorize]
public class LoanApplicationsController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-applications.manage";
    private const string ApprovePolicy = "Permission:loan-applications.approve";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly ILoanApplicationService _loanApplicationService;
    private readonly ILoanApplicationClientCodeService _clientCodes;

    public LoanApplicationsController(BCKashDbContext db, ILoanApplicationService loanApplicationService, ILoanApplicationClientCodeService clientCodes)
    {
        _db = db;
        _loanApplicationService = loanApplicationService;
        _clientCodes = clientCodes;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LoanApplicationListItemResponse>>> List(
        [FromQuery] ApprovalStatus? status,
        [FromQuery] int? clientId,
        [FromQuery] int? groupId,
        [FromQuery] int? officeId,
        [FromQuery] int? loanProductId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.LoanApplications.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status);
        }

        if (clientId.HasValue)
        {
            query = query.Where(a => a.ClientId == clientId);
        }

        if (groupId.HasValue)
        {
            query = query.Where(a => a.GroupId == groupId);
        }

        if (officeId.HasValue)
        {
            query = query.Where(a => a.OfficeId == officeId);
        }

        if (loanProductId.HasValue)
        {
            query = query.Where(a => a.LoanProductId == loanProductId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var applications = await query
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = applications.Select(ToListItemResponse).ToList();
        return Ok(new PagedResult<LoanApplicationListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanApplicationResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var application = await _db.LoanApplications.FindAsync([id], cancellationToken);
        return application is null ? NotFound() : Ok(ToResponse(application));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<LoanApplicationResponse>> Create(CreateLoanApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = new LoanApplication
        {
            ClientType = request.ClientType,
            LoanPurposeId = request.LoanPurposeId,
            CurrencyId = request.CurrencyId,
            OfficeId = request.OfficeId,
            ClientId = request.ClientId,
            GroupId = request.GroupId,
            LoanProductId = request.LoanProductId,
            Amount = request.Amount,
            LoanTerm = request.LoanTerm,
            LoanTermType = request.LoanTermType,
            Notes = request.Notes,
        };

        var result = await _loanApplicationService.CreateAsync(application, new ClientCodeSubmission(request.ClientCodeId, request.ClientCode), cancellationToken);

        return result.Outcome switch
        {
            LoanApplicationWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Application!.Id }, ToResponse(result.Application)),
            LoanApplicationWriteOutcome.ProductNotFound => Problem(title: "Loan product not found.", statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.AmountOutOfRange => Problem(
                title: "The requested amount is outside the loan product's configured range.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.TermOutOfRange => Problem(
                title: "The requested term is outside the loan product's configured range.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.ClientCodeRequired => ClientCodeProblem(
                "The client must confirm this application — send them a confirmation code and enter it.", "client_code_required"),
            LoanApplicationWriteOutcome.ClientCodeMismatch => ClientCodeProblem(
                "That code was sent for a different client, product or amount. Send the client a new code.", "client_code_mismatch"),
            LoanApplicationWriteOutcome.ClientCodeExpired => ClientCodeProblem(
                "That code has expired or was already used. Send the client a new code.", "client_code_expired"),
            LoanApplicationWriteOutcome.ClientCodeIncorrect => ClientCodeProblem(
                result.ClientCodeAttemptsLeft > 0
                    ? $"That code isn't right. {result.ClientCodeAttemptsLeft} attempt{(result.ClientCodeAttemptsLeft == 1 ? "" : "s")} left."
                    : "That code isn't right, and it can't be tried again. Send the client a new code.",
                "client_code_incorrect"),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Step one of raising a loan for a client when client confirmation codes are on (Settings →
    /// Notifications): texts/emails the client a code naming the staff member, product and amount.
    /// Returns <c>Required: false</c> when codes are off, so the portal can submit straight away.
    /// </summary>
    [HttpPost("client-codes")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientCodeResponse>> RequestClientCode(RequestClientCodeRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Problem(title: "Enter the amount before sending the client a code.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _clientCodes.RequestAsync(request.ClientId, request.LoanProductId, request.Amount, cancellationToken);
        return result.Outcome switch
        {
            ClientCodeRequestOutcome.NotRequired => Ok(new ClientCodeResponse(false, null, null, null, 0)),
            ClientCodeRequestOutcome.Sent => Ok(new ClientCodeResponse(true, result.CodeId, result.SentTo, result.ExpiresAtUtc, ILoanApplicationClientCodeService.ResendAfterSeconds)),
            ClientCodeRequestOutcome.ClientNotFound => Problem(title: "Client not found.", statusCode: StatusCodes.Status400BadRequest),
            ClientCodeRequestOutcome.ProductNotFound => Problem(title: "Loan product not found.", statusCode: StatusCodes.Status400BadRequest),
            ClientCodeRequestOutcome.NoContact => Problem(
                title: "This client has no mobile number or email on file, so they can't be sent a code. Add one to their record first.",
                statusCode: StatusCodes.Status400BadRequest),
            ClientCodeRequestOutcome.TooSoon => Problem(
                title: $"A code was just sent. You can send another in {result.RetryAfterSeconds} seconds.",
                statusCode: StatusCodes.Status429TooManyRequests),
            ClientCodeRequestOutcome.TooMany => Problem(
                title: "Too many codes have been sent to this client in the last hour. Try again later.",
                statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private ObjectResult ClientCodeProblem(string title, string reason) =>
        Problem(title: title, statusCode: StatusCodes.Status400BadRequest, extensions: new Dictionary<string, object?> { ["reason"] = reason });

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, UpdateLoanApplicationRequest request, CancellationToken cancellationToken)
    {
        var updated = new LoanApplication
        {
            ClientType = request.ClientType,
            LoanPurposeId = request.LoanPurposeId,
            CurrencyId = request.CurrencyId,
            OfficeId = request.OfficeId,
            ClientId = request.ClientId,
            GroupId = request.GroupId,
            LoanProductId = request.LoanProductId,
            Amount = request.Amount,
            LoanTerm = request.LoanTerm,
            LoanTermType = request.LoanTermType,
            Notes = request.Notes,
        };

        var result = await _loanApplicationService.UpdateAsync(id, updated, cancellationToken);

        return result.Outcome switch
        {
            LoanApplicationWriteOutcome.Success => Ok(ToResponse(result.Application!)),
            LoanApplicationWriteOutcome.NotFound => NotFound(),
            LoanApplicationWriteOutcome.ProductNotFound => Problem(title: "Loan product not found.", statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.AmountOutOfRange => Problem(
                title: "The requested amount is outside the loan product's configured range.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.TermOutOfRange => Problem(
                title: "The requested term is outside the loan product's configured range.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.InvalidTransition => Problem(
                title: "This application can no longer be edited — it isn't pending.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanApplicationWriteOutcome.ClientConfirmedTermsLocked => Problem(
                title: "The client confirmed this application's amount and product by code, so they can't be changed. Raise a new application instead.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-LN-5 — creates/links the resulting Loan record, traceable via `LoanApplication.LoanId`.</summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Approve(int id, ApproveLoanApplicationRequest request, CancellationToken cancellationToken)
    {
        var result = await _loanApplicationService.ApproveAsync(id, request.ApprovedAmount, request.Notes, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-6 — terminal; a declined application cannot be resurrected.</summary>
    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _loanApplicationService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    private IActionResult ToTransitionResult(LoanApplicationWriteResult result) => result.Outcome switch
    {
        LoanApplicationWriteOutcome.Success => Ok(ToResponse(result.Application!)),
        LoanApplicationWriteOutcome.NotFound => NotFound(),
        LoanApplicationWriteOutcome.ProductNotFound => Problem(title: "Loan product not found.", statusCode: StatusCodes.Status400BadRequest),
        LoanApplicationWriteOutcome.InsufficientOfficeFunds => Problem(title: result.Error, statusCode: StatusCodes.Status409Conflict),
        LoanApplicationWriteOutcome.InvalidTransition => Problem(
            title: "This transition isn't valid from the application's current status.",
            statusCode: StatusCodes.Status400BadRequest),
        LoanApplicationWriteOutcome.ReasonRequired => Problem(
            title: "A reason is required for this transition.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static LoanApplicationListItemResponse ToListItemResponse(LoanApplication a) =>
        new(a.Id, a.ClientType, a.ClientId, a.GroupId, a.OfficeId, a.LoanProductId, a.Amount, a.Status, a.LoanId);

    private static LoanApplicationResponse ToResponse(LoanApplication a) => new(
        a.Id, a.ClientType, a.UserId, a.LoanId, a.LoanPurposeId, a.CurrencyId,
        a.OfficeId, a.ClientId, a.GroupId, a.LoanProductId, a.Amount, a.Status,
        a.LoanTerm, a.LoanTermType, a.ApprovedById, a.DeclinedById,
        a.ApprovedNotes, a.DeclinedNotes, a.DeclinedDate, a.ApprovedDate, a.Notes);
}
