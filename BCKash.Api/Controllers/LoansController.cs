using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Application.Loans;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Clients;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Loans;
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

    private readonly IOfficeScope _scope;

    public LoansController(BCKashDbContext db, ILoanService loanService, ILoanWriteOffService writeOffService, ILoanNpaService npaService, IOfficeScope scope)
    {
        _scope = scope;
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
        [FromQuery] int? loanOfficerId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Loans.AsQueryable();
        query = await ScopedAsync(query, cancellationToken);

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

        if (loanOfficerId.HasValue)
        {
            query = query.Where(l => l.LoanOfficerId == loanOfficerId);
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

        return Ok(new PagedResult<LoanListItemResponse>(await ToNamedListItemsAsync(loans, cancellationToken), page, pageSize, totalCount));
    }

    /// <summary>
    /// Disbursed loans carrying an installment, due within the last year, whose principal isn't
    /// fully paid off yet — the drill-down behind the dashboard's "Late Loans" tile. See
    /// DashboardController.Summary's doc comment for why this is keyed off paid amounts and
    /// bounded to a year, not the `Paid`/`TotalDue` columns or all-time history.
    /// </summary>
    /// <summary>
    /// Charges any late-repayment and default penalties that have fallen due (see LoanPenaltyService).
    /// Also runs daily on its own; this is for running it now. Idempotent, and a no-op while
    /// "Apply penalties automatically" is off.
    /// </summary>
    /// <summary>The loan at a glance: principal, what it comes to with interest, when it should finish, what's been repaid and what's left, and its penalties.</summary>
    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<LoanSummaryResponse>> Summary(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        var schedule = await _db.LoanRepaymentSchedules.Where(s => s.LoanId == id).ToListAsync(cancellationToken);
        var repayments = await _db.LoanTransactions
            .Where(t => t.LoanId == id && t.TransactionType == LoanTransactionType.Repayment && !t.Reversed)
            .SumAsync(t => t.Amount ?? 0, cancellationToken);
        var saved = await _db.ClientSavingsEntries
            .Where(e => e.LoanId == id && (e.Type == ClientSavingsEntryType.Contribution || e.Type == ClientSavingsEntryType.ContributionReversal))
            .SumAsync(e => e.Amount, cancellationToken);

        decimal Sum(Func<LoanRepaymentSchedule, decimal?> field) => schedule.Sum(s => field(s) ?? 0);
        var hasSchedule = schedule.Count > 0;
        var principal = hasSchedule ? Sum(s => s.Principal) : loan.Principal ?? loan.ApprovedAmount;
        var interest = hasSchedule ? Sum(s => s.Interest) : (decimal?)null;
        var remaining = hasSchedule
            ? Math.Max(0, Sum(s => s.Principal) + Sum(s => s.Interest) + Sum(s => s.Fees) + Sum(s => s.Penalty)
                - Sum(s => s.PrincipalPaid) - Sum(s => s.InterestPaid) - Sum(s => s.FeesPaid) - Sum(s => s.PenaltyPaid)
                - Sum(s => s.PrincipalWaived) - Sum(s => s.InterestWaived) - Sum(s => s.FeesWaived) - Sum(s => s.PenaltyWaived)
                - Sum(s => s.PrincipalWrittenOff) - Sum(s => s.InterestWrittenOff) - Sum(s => s.FeesWrittenOff) - Sum(s => s.PenaltyWrittenOff))
            : (decimal?)null;
        var expected = hasSchedule ? principal + interest : null;

        // Penalties: charged on the schedule; started when the first one fell due.
        var charged = Sum(s => s.Penalty);
        var penaltyPaid = Sum(s => s.PenaltyPaid);
        var penaltyWaived = Sum(s => s.PenaltyWaived) + Sum(s => s.PenaltyWrittenOff);
        var penaltyOutstanding = Math.Max(0, charged - penaltyPaid - penaltyWaived);
        var startedOn = await _db.LoanPenaltyApplications.Where(p => p.LoanId == id).OrderBy(p => p.DueDate).Select(p => (DateOnly?)p.DueDate).FirstOrDefaultAsync(cancellationToken)
            ?? schedule.Where(s => (s.Penalty ?? 0) > 0).OrderBy(s => s.DueDate).Select(s => s.DueDate).FirstOrDefault();
        var penaltyStatus = charged <= 0 ? "None"
            : penaltyOutstanding <= 0 ? (penaltyPaid > 0 ? "Paid" : "Waived")
            : penaltyPaid > 0 ? "PartPaid"
            : "Unpaid";

        return Ok(new LoanSummaryResponse(
            principal,
            interest,
            expected,
            hasSchedule ? Sum(s => s.Fees) : null,
            hasSchedule ? schedule.Max(s => s.DueDate) : null,
            repayments,
            saved,
            remaining,
            loan.SavingsRate,
            expected is { } e ? ClientSavingsRules.GrossUp(e, loan.SavingsRate) : null,
            remaining is { } r ? ClientSavingsRules.GrossUp(r, loan.SavingsRate) : null,
            new LoanPenaltySummaryResponse(charged, penaltyPaid, penaltyWaived, penaltyOutstanding, startedOn, penaltyStatus),
            new LoanCompletionResponse(
                loan.Status is LoanStatus.Closed or LoanStatus.Paid,
                loan.Status is LoanStatus.Closed or LoanStatus.Paid ? loan.ClosedDate : null,
                schedule.Sum(s => LoanCompletionService.Remaining(s.Principal, s.PrincipalPaid, s.PrincipalWaived, s.PrincipalWrittenOff)),
                schedule.Sum(s => LoanCompletionService.Remaining(s.Interest, s.InterestPaid, s.InterestWaived, s.InterestWrittenOff)),
                schedule.Sum(s => LoanCompletionService.Remaining(s.Fees, s.FeesPaid, s.FeesWaived, s.FeesWrittenOff)),
                schedule.Sum(s => LoanCompletionService.Remaining(s.Penalty, s.PenaltyPaid, s.PenaltyWaived, s.PenaltyWrittenOff)))));
    }

    [HttpPost("penalties/run-due")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<PenaltyRunResult>> RunDuePenalties([FromServices] ILoanPenaltyService penalties, CancellationToken cancellationToken) =>
        Ok(await penalties.RunDueAsync(cancellationToken: cancellationToken));

    /// <summary>
    /// Sends the upcoming-repayment, missed-repayment and loan-overdue reminders that have fallen due
    /// (Settings → Notifications). Also runs daily on its own; each reminder goes only once.
    /// </summary>
    [HttpPost("reminders/run-due")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<ActionResult<LoanReminderRunResult>> RunDueReminders([FromServices] ILoanNotificationService notifications, CancellationToken cancellationToken) =>
        Ok(await notifications.SendDueRemindersAsync(cancellationToken: cancellationToken));

    [HttpGet("late")]
    public async Task<ActionResult<PagedResult<LoanListItemResponse>>> Late(
        [FromServices] IOverdueRulesProvider overdueRules,
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
        // Late only once the "repayment overdue after N days" threshold has passed (Settings → Loan).
        var overdueBefore = today.AddDays(-(await overdueRules.GetAsync(cancellationToken)).RepaymentOverdueDays);

        var lateLoanIds = _db.LoanRepaymentSchedules
            .Where(s => s.DueDate < overdueBefore && s.DueDate >= windowStart
                     && (s.Principal ?? 0) > (s.PrincipalPaid ?? 0)
                     && s.Loan!.Status == LoanStatus.Disbursed)
            .Select(s => s.LoanId)
            .Distinct();

        var query = _db.Loans.Where(l => lateLoanIds.Contains(l.Id));
        query = await ScopedAsync(query, cancellationToken);

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

        return Ok(new PagedResult<LoanListItemResponse>(await ToNamedListItemsAsync(loans, cancellationToken), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanResponse>> Get(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var loan = await _db.Loans.FindAsync([id], cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        var names = await LoanDisplayNames.LoadAsync(_db, [loan.ClientId], [loan.GroupId], [loan.LoanProductId], [loan.OfficeId], cancellationToken);
        return Ok(ToResponse(loan) with
        {
            ApplicantName = names.Applicant(loan.ClientId, loan.GroupId),
            LoanProductName = names.Product(loan.LoanProductId),
            OfficeName = names.Office(loan.OfficeId),
        });
    }

    /// <summary>The reviewer's action — sends a freshly-approved loan back to the loan officer.</summary>
    [HttpPost("{id:int}/request-changes")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> RequestChanges(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _loanService.RequestChangesAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>The loan officer's action — after revising terms, sends the loan back to Pending.</summary>
    [HttpPost("{id:int}/resubmit")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Resubmit(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _loanService.ResubmitAsync(id, cancellationToken);
        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-8's bare status transition — see LoanService.DisburseAsync's doc comment for the FR-LN-15 scope boundary (no schedule generated).</summary>
    [HttpPost("{id:int}/disburse")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> Disburse(
        int id,
        DisburseLoanRequest request,
        [FromServices] IClientBiometricsService biometrics,
        [FromServices] ILoanNotificationService notifications,
        [FromServices] IStaffNotificationService staff,
        CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        // While face capture is mandatory, everyone receiving money must first pass a face match against
        // their enrolled face. Users with no user_type (legacy accounts) keep their old behaviour, as
        // elsewhere (see IOfficeScope).
        if (await _scope.GetUserTypeAsync(cancellationToken) is not null && await FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken))
        {
            var unverified = (await biometrics.LoanChecksAsync(id, cancellationToken)).Where(c => !c.Verified).ToList();
            if (unverified.Count > 0)
            {
                return Problem(
                    title: $"Face match needed before disbursing: {string.Join(", ", unverified.Select(c => c.ClientName ?? $"client #{c.ClientId}"))}.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        var result = await _loanService.DisburseAsync(id, request.DisbursementDate, request.DisbursedAmount, request.Notes, cancellationToken);
        if (result.Outcome == LoanWriteOutcome.Success)
        {
            await notifications.LoanDisbursedAsync(id, cancellationToken);
            await staff.LoanDisbursedAsync(id, cancellationToken);
        }

        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-24: moves remaining outstanding amounts out of the active portfolio.</summary>
    [HttpPost("{id:int}/write-off")]
    [Authorize(Policy = ServicingPolicy)]
    public async Task<IActionResult> WriteOff(int id, WriteOffLoanRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

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
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

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
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

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
        LoanWriteOutcome.InsufficientOfficeFunds => Problem(title: result.Error, statusCode: StatusCodes.Status409Conflict),
        LoanWriteOutcome.InvalidTransition => Problem(
            title: "This transition isn't valid from the loan's current status.",
            statusCode: StatusCodes.Status400BadRequest),
        LoanWriteOutcome.ReasonRequired => Problem(
            title: "A reason is required for this transition.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private async Task<List<LoanListItemResponse>> ToNamedListItemsAsync(IReadOnlyList<Loan> loans, CancellationToken cancellationToken)
    {
        var names = await LoanDisplayNames.LoadAsync(
            _db, loans.Select(l => l.ClientId), loans.Select(l => l.GroupId), loans.Select(l => l.LoanProductId), loans.Select(l => l.OfficeId), cancellationToken);
        return loans
            .Select(l => ToListItemResponse(l) with
            {
                ApplicantName = names.Applicant(l.ClientId, l.GroupId),
                LoanProductName = names.Product(l.LoanProductId),
                OfficeName = names.Office(l.OfficeId),
            })
            .ToList();
    }

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
        l.IsNpa, l.IncomeSuspended, l.Notes,
        DisbursementMode: l.DisbursementMode, DisbursementBankName: l.DisbursementBankName,
        DisbursementAccountNumber: l.DisbursementAccountNumber, DisbursementAccountName: l.DisbursementAccountName);

    /// <summary>Only records in the caller's offices (see <see cref="IOfficeScope"/>); everything for a super admin.</summary>
    private async Task<IQueryable<Loan>> ScopedAsync(IQueryable<Loan> query, CancellationToken cancellationToken)
    {
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        return officeIds is null ? query : query.Where(l => l.OfficeId.HasValue && officeIds.Contains(l.OfficeId.Value));
    }

    /// <summary>False for a loan outside the caller's offices — callers answer 404, so it stays invisible.</summary>
    private async Task<bool> InScopeAsync(int id, CancellationToken cancellationToken) =>
        await (await ScopedAsync(_db.Loans.Where(l => l.Id == id), cancellationToken)).AnyAsync(cancellationToken);

    private ObjectResult OfficeOutOfScope() =>
        Problem(title: "You can only work with records in your own office(s).", statusCode: StatusCodes.Status403Forbidden);
}
