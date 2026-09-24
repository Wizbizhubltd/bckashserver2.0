using BCKash.Api.Contracts;
using BCKash.Application.Loans;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Loan read/list plus the lifecycle actions that live directly on the loan itself: FR-LN-7's
/// need-changes/pending cycle, FR-LN-8's disbursement (which now generates the schedule — see
/// LoanService.DisburseAsync), FR-LN-24's write-off/recovery, and FR-LN-25's NPA recompute.
/// Repayments/waivers/reschedule live in their own sub-resource controllers.
/// </summary>
[ApiController]
[Route("api/v1/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-applications.manage";
    private const string ApprovePolicy = "Permission:loan-applications.approve";

    /// <summary>Loan-servicing actions (disbursement, and later repayments/reschedule/write-off) get their own slug, distinct from origination's manage/approve split — matches the BRD's Loan Officer/Teller actor for disbursement.</summary>
    private const string ServicingPolicy = "Permission:loan-servicing.manage";

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly ILoanService _loanService;
    private readonly ILoanWriteOffService _writeOffService;
    private readonly ILoanNpaService _npaService;

    public LoansController(BCKashDbContext db, ILoanService loanService, ILoanWriteOffService writeOffService, ILoanNpaService npaService)
    {
        _db = db;
        _loanService = loanService;
        _writeOffService = writeOffService;
        _npaService = npaService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LoanListItemResponse>>> List(
        [FromQuery] LoanStatus? status,
        [FromQuery] int? clientId,
        [FromQuery] int? officeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Loans.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status);
        }

        if (clientId.HasValue)
        {
            query = query.Where(l => l.ClientId == clientId);
        }

        if (officeId.HasValue)
        {
            query = query.Where(l => l.OfficeId == officeId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.AccountNumber != null && l.AccountNumber.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var loans = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = loans.Select(ToListItemResponse).ToList();
        return Ok(new PagedResult<LoanListItemResponse>(items, page, pageSize, totalCount));
    }

    /// <summary>
    /// Disbursed loans carrying an installment, due within the last year, whose principal isn't
    /// fully paid off yet — the drill-down behind the dashboard's "Late Loans" tile. See
    /// DashboardController.Summary's doc comment for why this is keyed off paid amounts and
    /// bounded to a year, not the `Paid`/`TotalDue` columns or all-time history.
    /// </summary>
    [HttpGet("late")]
    public async Task<ActionResult<PagedResult<LoanListItemResponse>>> Late(
        [FromQuery] int? officeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddYears(-1);

        var lateLoanIds = _db.LoanRepaymentSchedules
            .Where(s => s.DueDate < today && s.DueDate >= windowStart
                     && (s.Principal ?? 0) > (s.PrincipalPaid ?? 0)
                     && s.Loan!.Status == LoanStatus.Disbursed)
            .Select(s => s.LoanId)
            .Distinct();

        var query = _db.Loans.Where(l => lateLoanIds.Contains(l.Id));

        if (officeId.HasValue)
        {
            query = query.Where(l => l.OfficeId == officeId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.AccountNumber != null && l.AccountNumber.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var loans = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = loans.Select(ToListItemResponse).ToList();
        return Ok(new PagedResult<LoanListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans.FindAsync([id], cancellationToken);
        return loan is null ? NotFound() : Ok(ToResponse(loan));
    }

    /// <summary>The reviewer's action — sends a freshly-approved loan back to the loan officer.</summary>
    [HttpPost("{id:int}/request-changes")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> RequestChanges(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _loanService.RequestChangesAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>The loan officer's action — after revising terms, sends the loan back to Pending.</summary>
    [HttpPost("{id:int}/resubmit")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Resubmit(int id, CancellationToken cancellationToken)
    {
        var result = await _loanService.ResubmitAsync(id, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-8's bare status transition — see LoanService.DisburseAsync's doc comment for the FR-LN-15 scope boundary (no schedule generated).</summary>
    [HttpPost("{id:int}/disburse")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Disburse(int id, DisburseLoanRequest request, CancellationToken cancellationToken)
    {
        var result = await _loanService.DisburseAsync(id, request.DisbursementDate, request.DisbursedAmount, request.Notes, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-24: moves remaining outstanding amounts out of the active portfolio.</summary>
    [HttpPost("{id:int}/write-off")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> WriteOff(int id, WriteOffLoanRequest request, CancellationToken cancellationToken)
    {
        var result = await _writeOffService.WriteOffAsync(id, request.Reason, request.Date, cancellationToken);

        return result.Outcome switch
        {
            LoanWriteOffOutcome.Success => Ok(ToResponse(result.Loan!)),
            LoanWriteOffOutcome.NotFound => NotFound(),
            LoanWriteOffOutcome.InvalidTransition => Problem(
                title: "This loan can't be written off from its current status.",
                statusCode: StatusCodes.Status400BadRequest),
            LoanWriteOffOutcome.ReasonRequired => Problem(title: "A reason is required to write off a loan.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-LN-24: a recovery payment against an already-written-off loan.</summary>
    [HttpPost("{id:int}/record-recovery")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> RecordRecovery(int id, RecordRecoveryRequest request, CancellationToken cancellationToken)
    {
        var result = await _writeOffService.RecordRecoveryAsync(id, request.Amount, request.Date, request.Notes, cancellationToken);

        return result.Outcome switch
        {
            LoanWriteOffOutcome.Success => Ok(ToResponse(result.Loan!)),
            LoanWriteOffOutcome.NotFound => NotFound(),
            LoanWriteOffOutcome.InvalidTransition => Problem(title: "Only a written-off loan can record a recovery.", statusCode: StatusCodes.Status400BadRequest),
            LoanWriteOffOutcome.InvalidAmount => Problem(title: "Recovery amount must be greater than zero.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-LN-25: on-demand NPA recompute — no job scheduler exists to run this periodically (see ILoanNpaService's doc comment).</summary>
    [HttpPost("{id:int}/recompute-npa")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<NpaStatusResponse>> RecomputeNpa(int id, CancellationToken cancellationToken)
    {
        var result = await _npaService.RecomputeAsync(id, cancellationToken);

        return result.Outcome switch
        {
            LoanNpaOutcome.Success => Ok(new NpaStatusResponse(result.IsNpa, result.IncomeSuspended, result.DaysInArrears)),
            LoanNpaOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private IActionResult ToTransitionResult(LoanWriteResult result) => result.Outcome switch
    {
        LoanWriteOutcome.Success => Ok(ToResponse(result.Loan!)),
        LoanWriteOutcome.NotFound => NotFound(),
        LoanWriteOutcome.InvalidTransition => Problem(
            title: "This transition isn't valid from the loan's current status.",
            statusCode: StatusCodes.Status400BadRequest),
        LoanWriteOutcome.ReasonRequired => Problem(
            title: "A reason is required for this transition.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static LoanListItemResponse ToListItemResponse(Loan l) =>
        new(l.Id, l.AccountNumber, l.ClientId, l.GroupId, l.OfficeId, l.LoanProductId, l.AppliedAmount, l.ApprovedAmount, l.Status);

    private static LoanResponse ToResponse(Loan l) => new(
        l.Id, l.ClientType, l.LoanProductId, l.ClientId, l.OfficeId, l.GroupId,
        l.LoanPurposeId, l.CurrencyId, l.AccountNumber, l.Principal, l.AppliedAmount, l.ApprovedAmount,
        l.LoanTerm, l.LoanTermType, l.InterestRate, l.InterestRateType,
        l.InterestMethod, l.AmortizationMethod, l.Status,
        l.ApprovedById, l.ApprovedDate, l.ApprovedNotes,
        l.NeedChangesById, l.NeedChangesDate,
        l.DisbursementDate, l.DisbursedById, l.DisbursedNotes,
        l.WrittenOffDate, l.WrittenOffNotes,
        l.IsNpa, l.IncomeSuspended, l.Notes);
}
