using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/loan-applications")]
[Authorize]
public class LoanApplicationsController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-applications.manage";
    private const string ApprovePolicy = "Permission:loan-applications.approve";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly ILoanApplicationService _loanApplicationService;

    public LoanApplicationsController(BCKashDbContext db, ILoanApplicationService loanApplicationService)
    {
        _db = db;
        _loanApplicationService = loanApplicationService;
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

        var result = await _loanApplicationService.CreateAsync(application, cancellationToken);

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
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

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
