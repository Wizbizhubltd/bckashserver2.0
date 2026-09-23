using BCKash.Api.Contracts;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/gl/journal-entries")]
[Authorize]
public class GlJournalEntriesController : ControllerBase
{
    private const string ManagePolicy = "Permission:gl.manage";

    private readonly BCKashDbContext _db;
    private readonly IManualJournalEntryService _manualEntryService;
    private readonly IGlJournalEntryService _journalEntryService;

    public GlJournalEntriesController(BCKashDbContext db, IManualJournalEntryService manualEntryService, IGlJournalEntryService journalEntryService)
    {
        _db = db;
        _manualEntryService = manualEntryService;
        _journalEntryService = journalEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GlJournalEntryResponse>>> List(
        [FromQuery] int? officeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] int? glAccountId, [FromQuery] string? reference, CancellationToken cancellationToken)
    {
        var query = _db.GlJournalEntries.Include(e => e.GlAccount).AsQueryable();

        if (officeId.HasValue)
        {
            query = query.Where(e => e.OfficeId == officeId);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.Date >= fromDate);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.Date <= toDate);
        }

        if (glAccountId.HasValue)
        {
            query = query.Where(e => e.GlAccountId == glAccountId);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            query = query.Where(e => e.Reference == reference);
        }

        var entries = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToListAsync(cancellationToken);
        return Ok(entries.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyCollection<GlJournalEntryResponse>>> Create(CreateManualJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var lines = request.Lines.Select(l => new ManualJournalEntryLine(l.GlAccountId, l.Debit, l.Credit)).ToList();
        var result = await _manualEntryService.CreateAsync(request.OfficeId, request.Date, lines, request.Narration, cancellationToken);

        return result.Outcome switch
        {
            ManualJournalEntryOutcome.Success => Ok(result.Entries!.Select(ToResponse).ToList()),
            ManualJournalEntryOutcome.TooFewLines => Problem(title: "A manual journal entry needs at least two lines.", statusCode: StatusCodes.Status400BadRequest),
            ManualJournalEntryOutcome.LineAmountInvalid => Problem(title: "Each line must have exactly one of debit or credit set, and it must be positive.", statusCode: StatusCodes.Status400BadRequest),
            ManualJournalEntryOutcome.Unbalanced => Problem(title: "Total debits must equal total credits.", statusCode: StatusCodes.Status400BadRequest),
            ManualJournalEntryOutcome.AccountNotFound => Problem(title: "One or more GL accounts were not found.", statusCode: StatusCodes.Status400BadRequest),
            ManualJournalEntryOutcome.ManualEntriesNotAllowed => Problem(title: "One or more target accounts do not allow manual entries.", statusCode: StatusCodes.Status400BadRequest),
            ManualJournalEntryOutcome.ClosurePeriod => Problem(title: "This date falls within a closed accounting period for this office.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{reference}/approve")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyCollection<GlJournalEntryResponse>>> Approve(string reference, ApproveJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _manualEntryService.ApproveAsync(reference, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            ManualJournalEntryOutcome.Success => Ok(result.Entries!.Select(ToResponse).ToList()),
            ManualJournalEntryOutcome.NotFound => NotFound(),
            ManualJournalEntryOutcome.AlreadyApproved => Problem(title: "This journal entry has already been approved.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{reference}/reverse")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<IReadOnlyCollection<GlJournalEntryResponse>>> Reverse(string reference, CancellationToken cancellationToken)
    {
        var result = await _journalEntryService.ReverseByReferenceAsync(reference, cancellationToken);
        return result.Outcome switch
        {
            GlJournalEntryReversalOutcome.Success => Ok(result.Entries!.Select(ToResponse).ToList()),
            GlJournalEntryReversalOutcome.NotFound => NotFound(),
            GlJournalEntryReversalOutcome.AlreadyReversed => Problem(title: "This journal entry has already been reversed.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static GlJournalEntryResponse ToResponse(GlJournalEntry e) => new(
        e.Id, e.OfficeId, e.GlAccountId, e.GlAccount?.Name, e.TransactionType, e.TransactionSubType, e.Debit, e.Credit,
        e.Reversed, e.Reference, e.LoanId, e.LoanTransactionId, e.SavingsId, e.SavingsTransactionId, e.Date, e.Narration,
        e.ManualEntry, e.Approved, e.ApprovedById, e.ApprovedDate, e.ApprovedNotes);
}
