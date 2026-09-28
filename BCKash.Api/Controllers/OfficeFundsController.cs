using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using BCKash.Application.Files;
using BCKash.Application.Organization;
using BCKash.Domain.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Office business operations (see IOfficeFundService): bank accounts and funding are managed by
/// super admins; funding is acknowledged or disputed by the office's assigned manager. Super admins
/// and that manager can view an office's business operations.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class OfficeFundsController : ControllerBase
{
    private const int HistoryLimit = 100;

    private readonly BCKashDbContext _db;
    private readonly IOfficeFundService _funds;
    private readonly IFileStorageService _files;
    private readonly ICurrentUserContext _currentUser;

    public OfficeFundsController(BCKashDbContext db, IOfficeFundService funds, IFileStorageService files, ICurrentUserContext currentUser)
    {
        _db = db;
        _funds = funds;
        _files = files;
        _currentUser = currentUser;
    }

    /// <summary>The banks an office account can be held with (see NigerianBanks), grouped by licence type.</summary>
    [HttpGet("banks")]
    public ActionResult<IReadOnlyList<BankResponse>> Banks() =>
        Ok(NigerianBanks.All.Select(b => new BankResponse(b.Name, b.Category)).ToList());

    [HttpGet("offices/{officeId:int}/business-operations")]
    public async Task<ActionResult<OfficeBusinessOperationsResponse>> Get(int officeId, CancellationToken cancellationToken)
    {
        var summary = await _funds.GetSummaryAsync(officeId, cancellationToken);
        if (summary is null)
        {
            return NotFound();
        }

        if (!IsSuperAdmin && summary.ManagerId != _currentUser.UserId)
        {
            return Forbid();
        }

        var accounts = await _db.OfficeBankAccounts.Where(a => a.OfficeId == officeId).OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        var fundings = await _db.OfficeFundings.Where(f => f.OfficeId == officeId).OrderByDescending(f => f.Id).Take(HistoryLimit).ToListAsync(cancellationToken);
        var entries = await _db.OfficeFundEntries.Where(e => e.OfficeId == officeId).OrderByDescending(e => e.Id).Take(HistoryLimit).ToListAsync(cancellationToken);
        var events = await _db.OfficeFundEvents.Where(e => e.OfficeId == officeId).OrderByDescending(e => e.Id).Take(HistoryLimit).ToListAsync(cancellationToken);

        var names = await NamesAsync(
            fundings.SelectMany(f => new[] { f.FundedById, f.AcknowledgedById, f.DisputedById, f.CancelledById })
                .Concat(entries.Select(e => e.CreatedById)).Concat(events.Select(e => e.ActorId)).Append(summary.ManagerId),
            cancellationToken);
        var officeName = await _db.Offices.Where(o => o.Id == officeId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken);

        return Ok(new OfficeBusinessOperationsResponse(
            officeId, summary.Balance, summary.Committed, summary.Available, summary.PendingAmount, summary.PendingCount,
            summary.ManagerId, Name(names, summary.ManagerId), summary.LoansRequireFunds, summary.ManagerId == _currentUser.UserId,
            accounts.Select(ToResponse).ToList(),
            fundings.Select(f => ToResponse(f, officeName, accounts, names)).ToList(),
            entries.Select(e => new OfficeFundEntryResponse(e.Id, e.Type, e.Amount, e.BalanceAfter, e.FundingId, e.LoanId, e.Description, Name(names, e.CreatedById), e.CreatedAt)).ToList(),
            events.Select(e => new OfficeFundEventResponse(e.Id, e.Type, e.FundingId, e.Amount, e.Comment, Name(names, e.ActorId), e.CreatedAt)).ToList()));
    }

    /// <summary>Funding for the offices the current user manages — the office portal's acknowledgement queue.</summary>
    [HttpGet("office-fundings/mine")]
    public async Task<ActionResult<IReadOnlyList<OfficeFundingResponse>>> Mine(CancellationToken cancellationToken)
    {
        var offices = await _db.Offices.Where(o => o.ManagerId == _currentUser.UserId).Select(o => new { o.Id, o.Name }).ToListAsync(cancellationToken);
        var officeIds = offices.Select(o => o.Id).ToList();
        var fundings = await _db.OfficeFundings.Where(f => officeIds.Contains(f.OfficeId)).OrderByDescending(f => f.Id).Take(HistoryLimit).ToListAsync(cancellationToken);
        var accountIds = fundings.Where(f => f.BankAccountId.HasValue).Select(f => f.BankAccountId!.Value).Distinct().ToList();
        var accounts = await _db.OfficeBankAccounts.Where(a => accountIds.Contains(a.Id)).ToListAsync(cancellationToken);
        var names = await NamesAsync(fundings.SelectMany(f => new[] { f.FundedById, f.AcknowledgedById, f.DisputedById, f.CancelledById }), cancellationToken);

        return Ok(fundings.Select(f => ToResponse(f, offices.First(o => o.Id == f.OfficeId).Name, accounts, names)).ToList());
    }

    [HttpPost("offices/{officeId:int}/bank-accounts")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<OfficeBankAccountResponse>> AddBankAccount(int officeId, SaveOfficeBankAccountRequest request, CancellationToken cancellationToken) =>
        ToResult(await _funds.AddBankAccountAsync(officeId, request.BankName, request.AccountName, request.AccountNumber, request.MakeDefault, cancellationToken), ToResponse);

    [HttpPost("offices/{officeId:int}/bank-accounts/{accountId:int}/default")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<OfficeBankAccountResponse>> SetDefault(int officeId, int accountId, CancellationToken cancellationToken) =>
        ToResult(await _funds.SetDefaultBankAccountAsync(officeId, accountId, cancellationToken), ToResponse);

    [HttpPost("offices/{officeId:int}/bank-accounts/{accountId:int}/deactivate")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<OfficeBankAccountResponse>> DeactivateBankAccount(int officeId, int accountId, CancellationToken cancellationToken) =>
        ToResult(await _funds.DeactivateBankAccountAsync(officeId, accountId, cancellationToken), ToResponse);

    [HttpPost("offices/{officeId:int}/fundings")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<OfficeFundingResponse>> Fund(int officeId, FundOfficeRequest request, CancellationToken cancellationToken) =>
        await FundingResultAsync(await _funds.FundAsync(officeId, request.Amount, request.Reference, request.FundedOn, request.Notes, cancellationToken), cancellationToken);

    [HttpPost("office-fundings/{fundingId:int}/acknowledge")]
    public async Task<ActionResult<OfficeFundingResponse>> Acknowledge(int fundingId, FundingCommentRequest request, CancellationToken cancellationToken) =>
        await FundingResultAsync(await _funds.AcknowledgeAsync(fundingId, request.Comment, cancellationToken), cancellationToken);

    /// <summary>Multipart: <c>reason</c> and the bank statement <c>statement</c> (PDF/JPG/PNG, up to 10 MB).</summary>
    [HttpPost("office-fundings/{fundingId:int}/dispute")]
    [RequestSizeLimit(IOfficeFundService.MaxStatementBytes + 1024 * 1024)]
    public async Task<ActionResult<OfficeFundingResponse>> Dispute(int fundingId, [FromForm] string? reason, IFormFile? statement, CancellationToken cancellationToken)
    {
        await using var stream = statement?.OpenReadStream();
        var upload = statement is null || stream is null ? null : new DisputeUpload(stream, statement.FileName, statement.Length);
        return await FundingResultAsync(await _funds.DisputeAsync(fundingId, reason, upload, cancellationToken), cancellationToken);
    }

    [HttpPost("office-fundings/{fundingId:int}/cancel")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<OfficeFundingResponse>> Cancel(int fundingId, FundingCommentRequest request, CancellationToken cancellationToken) =>
        await FundingResultAsync(await _funds.CancelAsync(fundingId, request.Comment, cancellationToken), cancellationToken);

    /// <summary>The bank statement attached to a dispute — for super admins and the office's manager.</summary>
    [HttpGet("office-fundings/{fundingId:int}/statement")]
    public async Task<IActionResult> Statement(int fundingId, CancellationToken cancellationToken)
    {
        var funding = await _db.OfficeFundings.FirstOrDefaultAsync(f => f.Id == fundingId, cancellationToken);
        if (funding?.DisputeDocumentLocation is null)
        {
            return NotFound();
        }

        var managerId = await _db.Offices.Where(o => o.Id == funding.OfficeId).Select(o => o.ManagerId).FirstOrDefaultAsync(cancellationToken);
        if (!IsSuperAdmin && managerId != _currentUser.UserId)
        {
            return Forbid();
        }

        var content = await _files.ReadAsync(funding.DisputeDocumentLocation, funding.DisputeDocumentName ?? "statement", cancellationToken);
        return content is null ? NotFound() : File(content.Content, content.ContentType, funding.DisputeDocumentName);
    }

    private bool IsSuperAdmin => User.HasClaim(AuthClaimTypes.UserType, UserTypeSlugs.SuperAdmin);

    private async Task<ActionResult<OfficeFundingResponse>> FundingResultAsync(OfficeFundResult<OfficeFunding> result, CancellationToken cancellationToken)
    {
        if (result.Outcome != OfficeFundOutcome.Success)
        {
            return Failure(result.Outcome, result.Error);
        }

        var funding = result.Value!;
        var officeName = await _db.Offices.Where(o => o.Id == funding.OfficeId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken);
        var accounts = await _db.OfficeBankAccounts.Where(a => a.Id == funding.BankAccountId).ToListAsync(cancellationToken);
        var names = await NamesAsync([funding.FundedById, funding.AcknowledgedById, funding.DisputedById, funding.CancelledById], cancellationToken);
        return Ok(ToResponse(funding, officeName, accounts, names));
    }

    private ActionResult<TOut> ToResult<TIn, TOut>(OfficeFundResult<TIn> result, Func<TIn, TOut> map) =>
        result.Outcome == OfficeFundOutcome.Success ? Ok(map(result.Value!)) : Failure(result.Outcome, result.Error);

    private ObjectResult Failure(OfficeFundOutcome outcome, string? error) => outcome switch
    {
        OfficeFundOutcome.NotFound => Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound),
        OfficeFundOutcome.NotOfficeManager => Problem(title: error, statusCode: StatusCodes.Status403Forbidden),
        OfficeFundOutcome.InvalidTransition => Problem(title: error, statusCode: StatusCodes.Status409Conflict),
        _ => Problem(title: error, statusCode: StatusCodes.Status400BadRequest),
    };

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        return await _db.Users
            .Where(u => wanted.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);
    }

    private static string? Name(Dictionary<int, string> names, int? id) => id.HasValue ? names.GetValueOrDefault(id.Value) : null;

    private static OfficeBankAccountResponse ToResponse(OfficeBankAccount a) =>
        new(a.Id, a.BankName, a.AccountName, a.AccountNumber, a.IsDefault, a.Active, a.CreatedAt);

    private static OfficeFundingResponse ToResponse(OfficeFunding f, string? officeName, IEnumerable<OfficeBankAccount> accounts, Dictionary<int, string> names)
    {
        var account = accounts.FirstOrDefault(a => a.Id == f.BankAccountId);
        return new OfficeFundingResponse(
            f.Id, f.OfficeId, officeName, f.Amount, f.Reference, f.FundedOn,
            account is null ? null : $"{account.BankName} ••••{account.AccountNumber[^Math.Min(4, account.AccountNumber.Length)..]}",
            f.Notes, f.Status, Name(names, f.FundedById), f.CreatedAt,
            Name(names, f.AcknowledgedById), f.AcknowledgedAt,
            Name(names, f.DisputedById), f.DisputedAt, f.DisputeReason, f.DisputeDocumentName,
            Name(names, f.CancelledById), f.CancelledAt, f.CancelReason);
    }
}
