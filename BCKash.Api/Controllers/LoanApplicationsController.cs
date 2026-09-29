using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Loans;
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

    private readonly IOfficeScope _scope;

    public LoanApplicationsController(BCKashDbContext db, ILoanApplicationService loanApplicationService, ILoanApplicationClientCodeService clientCodes, IOfficeScope scope)
    {
        _scope = scope;
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
        query = await ScopedAsync(query, cancellationToken);

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

        var names = await LoanDisplayNames.LoadAsync(
            _db, applications.Select(a => a.ClientId), applications.Select(a => a.GroupId),
            applications.Select(a => (int?)a.LoanProductId), applications.Select(a => a.OfficeId), cancellationToken);
        var items = applications
            .Select(a => ToListItemResponse(a) with
            {
                ApplicantName = names.Applicant(a.ClientId, a.GroupId),
                LoanProductName = names.Product(a.LoanProductId),
                OfficeName = names.Office(a.OfficeId),
            })
            .ToList();
        return Ok(new PagedResult<LoanApplicationListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanApplicationResponse>> Get(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var application = await _db.LoanApplications.FindAsync([id], cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        var names = await LoanDisplayNames.LoadAsync(_db, [application.ClientId], [application.GroupId], [application.LoanProductId], [application.OfficeId], cancellationToken);
        return Ok(ToResponse(application) with
        {
            ApplicantName = names.Applicant(application.ClientId, application.GroupId),
            LoanProductName = names.Product(application.LoanProductId),
            OfficeName = names.Office(application.OfficeId),
        });
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<LoanApplicationResponse>> Create(
        CreateLoanApplicationRequest request,
        [FromServices] IClientAccess access,
        [FromServices] IStaffNotificationService notifications,
        [FromServices] ICurrencyDisplayProvider currency,
        CancellationToken cancellationToken)
    {
        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

        // Only an approved client (or group) whose face is captured can apply for a loan — see
        // LoanApplicantRules. Users with no user_type keep their old behaviour, as elsewhere (see IOfficeScope).
        if (await _scope.GetUserTypeAsync(cancellationToken) is not null)
        {
            if (await LoanApplicantRules.ProblemAsync(_db, request.ClientType, request.ClientId, request.GroupId, cancellationToken) is { } applicantProblem)
            {
                return Problem(title: applicantProblem, statusCode: StatusCodes.Status409Conflict);
            }

            // One loan at a time: nothing new while a loan or application is still open.
            var activeLoan = request.ClientType == LoanClientType.Group
                ? await GroupActiveLoanAsync(request.GroupId, cancellationToken)
                : await access.ActiveLoanAsync(request.ClientId!.Value, cancellationToken);
            if (activeLoan is not null)
            {
                return Problem(title: $"{(request.ClientType == LoanClientType.Group ? "This group" : "This client")} already has an active loan. {activeLoan}", statusCode: StatusCodes.Status409Conflict);
            }
        }

        if (await PayoutProblemAsync(request.DisbursementMode, request.DisbursementBankName, request.DisbursementAccountNumber, request.DisbursementAccountName, cancellationToken) is { } payoutProblem)
        {
            return payoutProblem;
        }

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
        var (bankName, accountNumber, accountName) = Payout(request.DisbursementMode, request.DisbursementBankName, request.DisbursementAccountNumber, request.DisbursementAccountName);
        application.DisbursementMode = request.DisbursementMode;
        application.DisbursementBankName = bankName;
        application.DisbursementAccountNumber = accountNumber;
        application.DisbursementAccountName = accountName;

        var result = await _loanApplicationService.CreateAsync(application, new ClientCodeSubmission(request.ClientCodeId, request.ClientCode), cancellationToken);
        if (result.Outcome == LoanApplicationWriteOutcome.Success)
        {
            await notifications.LoanApplicationRaisedAsync(
                result.Application!, await ApplicantAsync(result.Application!, cancellationToken), (await currency.GetAsync(cancellationToken)).Format(result.Application!.Amount), cancellationToken);
        }

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

        // Don't text a client a code for a loan they can't take.
        if (await _scope.GetUserTypeAsync(cancellationToken) is not null
            && await LoanApplicantRules.ProblemAsync(_db, LoanClientType.Client, request.ClientId, null, cancellationToken) is { } applicantProblem)
        {
            return Problem(title: applicantProblem, statusCode: StatusCodes.Status409Conflict);
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
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

        if (await _scope.GetUserTypeAsync(cancellationToken) is not null
            && await LoanApplicantRules.ProblemAsync(_db, request.ClientType, request.ClientId, request.GroupId, cancellationToken) is { } applicantProblem)
        {
            return Problem(title: applicantProblem, statusCode: StatusCodes.Status409Conflict);
        }

        if (request.DisbursementMode is not null
            && await PayoutProblemAsync(request.DisbursementMode, request.DisbursementBankName, request.DisbursementAccountNumber, request.DisbursementAccountName, cancellationToken) is { } payoutProblem)
        {
            return payoutProblem;
        }

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
        var (bankName, accountNumber, accountName) = Payout(request.DisbursementMode, request.DisbursementBankName, request.DisbursementAccountNumber, request.DisbursementAccountName);
        updated.DisbursementMode = request.DisbursementMode;
        updated.DisbursementBankName = bankName;
        updated.DisbursementAccountNumber = accountNumber;
        updated.DisbursementAccountName = accountName;

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
    public async Task<IActionResult> Approve(
        int id, ApproveLoanApplicationRequest request, [FromServices] ILoanNotificationService notifications, [FromServices] IStaffNotificationService staff, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        // The applicant may have gone back to pending (e.g. after an edit) since the application was raised.
        if (await _scope.GetUserTypeAsync(cancellationToken) is not null)
        {
            var applicant = await _db.LoanApplications.Where(a => a.Id == id).Select(a => new { a.ClientType, a.ClientId, a.GroupId, a.Status }).FirstAsync(cancellationToken);
            if (applicant.Status == ApprovalStatus.Pending
                && await LoanApplicantRules.ProblemAsync(_db, applicant.ClientType, applicant.ClientId, applicant.GroupId, cancellationToken) is { } applicantProblem)
            {
                return Problem(title: applicantProblem, statusCode: StatusCodes.Status409Conflict);
            }
        }

        var result = await _loanApplicationService.ApproveAsync(id, request.ApprovedAmount, request.Notes, cancellationToken);
        if (result.Outcome == LoanApplicationWriteOutcome.Success && result.Application!.LoanId is { } loanId)
        {
            await notifications.LoanApprovedAsync(loanId, cancellationToken);
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
            await staff.LoanApplicationReviewedAsync(result.Application, loan, await ApplicantAsync(result.Application, cancellationToken), approved: true, cancellationToken);
        }

        return ToTransitionResult(result);
    }

    /// <summary>FR-LN-6 — terminal; a declined application cannot be resurrected.</summary>
    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, [FromServices] IStaffNotificationService staff, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _loanApplicationService.DeclineAsync(id, request.Reason, cancellationToken);
        if (result.Outcome == LoanApplicationWriteOutcome.Success)
        {
            await staff.LoanApplicationReviewedAsync(result.Application!, null, await ApplicantAsync(result.Application!, cancellationToken), approved: false, cancellationToken);
        }

        return ToTransitionResult(result);
    }

    /// <summary>The client's or group's name, for notifications.</summary>
    private async Task<string> ApplicantAsync(LoanApplication application, CancellationToken cancellationToken) =>
        (await LoanDisplayNames.LoadAsync(_db, [application.ClientId], [application.GroupId], [], [], cancellationToken)).Applicant(application.ClientId, application.GroupId)
        ?? (application.ClientType == LoanClientType.Group ? $"Group #{application.GroupId}" : $"Client #{application.ClientId}");

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

    private static readonly List<LoanStatus> OpenLoanStatuses =
    [
        LoanStatus.New, LoanStatus.Pending, LoanStatus.Approved, LoanStatus.NeedChanges,
        LoanStatus.Disbursed, LoanStatus.PendingReschedule, LoanStatus.Rescheduled,
    ];

    /// <summary>The group's own loan or application still open, or null.</summary>
    private async Task<string?> GroupActiveLoanAsync(int? groupId, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans
            .Where(l => l.GroupId == groupId && OpenLoanStatuses.Contains(l.Status))
            .Select(l => new { l.Id, l.AccountNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (loan is not null)
        {
            return $"Loan {loan.AccountNumber ?? $"#{loan.Id}"} is still open.";
        }

        var application = await _db.LoanApplications
            .Where(a => a.GroupId == groupId && a.Status == ApprovalStatus.Pending)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return application is null ? null : $"Loan application #{application} is awaiting a decision.";
    }

    private static LoanApplicationListItemResponse ToListItemResponse(LoanApplication a) =>
        new(a.Id, a.ClientType, a.ClientId, a.GroupId, a.OfficeId, a.LoanProductId, a.Amount, a.Status, a.LoanId);

    private static LoanApplicationResponse ToResponse(LoanApplication a) => new(
        a.Id, a.ClientType, a.UserId, a.LoanId, a.LoanPurposeId, a.CurrencyId,
        a.OfficeId, a.ClientId, a.GroupId, a.LoanProductId, a.Amount, a.Status,
        a.LoanTerm, a.LoanTermType, a.ApprovedById, a.DeclinedById,
        a.ApprovedNotes, a.DeclinedNotes, a.DeclinedDate, a.ApprovedDate, a.Notes,
        DisbursementMode: a.DisbursementMode, DisbursementBankName: a.DisbursementBankName,
        DisbursementAccountNumber: a.DisbursementAccountNumber, DisbursementAccountName: a.DisbursementAccountName,
        FormFee: a.FormFee);

    /// <summary>
    /// The payout details problem, or null when they're fine. Staff with a user_type must choose a mode;
    /// users with none keep their old behaviour (see IOfficeScope) unless they send one.
    /// </summary>
    private async Task<ObjectResult?> PayoutProblemAsync(DisbursementMode? mode, string? bankName, string? accountNumber, string? accountName, CancellationToken cancellationToken)
    {
        if (mode is null && await _scope.GetUserTypeAsync(cancellationToken) is null)
        {
            return null;
        }

        var problem = DisbursementRules.Validate(mode, bankName, accountNumber, accountName);
        return problem is null ? null : Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>Bank details only mean something for a bank transfer.</summary>
    private static (string? Bank, string? Number, string? Name) Payout(DisbursementMode? mode, string? bankName, string? accountNumber, string? accountName) =>
        mode == DisbursementMode.BankTransfer ? (bankName?.Trim(), accountNumber?.Trim(), accountName?.Trim()) : (null, null, null);

    /// <summary>Only records in the caller's offices (see <see cref="IOfficeScope"/>); everything for a super admin.</summary>
    private async Task<IQueryable<LoanApplication>> ScopedAsync(IQueryable<LoanApplication> query, CancellationToken cancellationToken)
    {
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        return officeIds is null ? query : query.Where(a => a.OfficeId.HasValue && officeIds.Contains(a.OfficeId.Value));
    }

    /// <summary>False for a loan application outside the caller's offices — callers answer 404, so it stays invisible.</summary>
    private async Task<bool> InScopeAsync(int id, CancellationToken cancellationToken) =>
        await (await ScopedAsync(_db.LoanApplications.Where(a => a.Id == id), cancellationToken)).AnyAsync(cancellationToken);

    private ObjectResult OfficeOutOfScope() =>
        Problem(title: "You can only work with records in your own office(s).", statusCode: StatusCodes.Status403Forbidden);
}
