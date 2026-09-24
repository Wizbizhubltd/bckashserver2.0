using BCKash.Api.Contracts;
using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/savings-accounts")]
[Authorize]
public class SavingsAccountsController : ControllerBase
{
    private const string ManagePolicy = "Permission:savings-accounts.manage";

    private readonly BCKashDbContext _db;
    private readonly ISavingsAccountService _accountService;

    public SavingsAccountsController(BCKashDbContext db, ISavingsAccountService accountService)
    {
        _db = db;
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavingsAccountResponse>>> List([FromQuery] int? clientId, CancellationToken cancellationToken)
    {
        var query = _db.Savings.AsQueryable();
        if (clientId.HasValue)
        {
            query = query.Where(s => s.ClientId == clientId);
        }

        var accounts = await query.ToListAsync(cancellationToken);
        return Ok(accounts.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SavingsAccountResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var account = await _db.Savings.FindAsync([id], cancellationToken);
        return account is null ? NotFound() : Ok(ToResponse(account));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SavingsAccountResponse>> Create(OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var account = new SavingsAccount
        {
            ClientType = request.ClientType,
            ClientId = request.ClientId,
            GroupId = request.GroupId,
            OfficeId = request.OfficeId,
            SavingsProductId = request.SavingsProductId,
            Notes = request.Notes,
        };

        var result = await _accountService.CreateAsync(account, cancellationToken);
        return result.Outcome switch
        {
            SavingsAccountWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Account!.Id }, ToResponse(result.Account)),
            SavingsAccountWriteOutcome.ProductNotFound => Problem(title: "Savings product not found.", statusCode: StatusCodes.Status400BadRequest),
            SavingsAccountWriteOutcome.AccountNumberGenerationFailed => Problem(statusCode: StatusCodes.Status500InternalServerError),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Approve(int id, ApproveSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.ApproveAsync(id, request.OpeningBalance, request.OverdraftLimit, request.Date, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsAccountWriteOutcome.Success => Ok(ToResponse(result.Account!)),
            SavingsAccountWriteOutcome.NotFound => NotFound(),
            SavingsAccountWriteOutcome.ProductNotFound => Problem(title: "Savings product not found.", statusCode: StatusCodes.Status400BadRequest),
            SavingsAccountWriteOutcome.InvalidTransition => Problem(title: "Only a pending account can be approved.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Decline(int id, DeclineSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeclineAsync(id, request.Reason, cancellationToken);
        return result.Outcome switch
        {
            SavingsAccountWriteOutcome.Success => Ok(ToResponse(result.Account!)),
            SavingsAccountWriteOutcome.NotFound => NotFound(),
            SavingsAccountWriteOutcome.ReasonRequired => Problem(title: "A decline reason is required.", statusCode: StatusCodes.Status400BadRequest),
            SavingsAccountWriteOutcome.InvalidTransition => Problem(title: "Only a pending account can be declined.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(int id, [FromBody] string? notes, CancellationToken cancellationToken)
    {
        var result = await _accountService.CloseAsync(id, notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsAccountWriteOutcome.Success => Ok(ToResponse(result.Account!)),
            SavingsAccountWriteOutcome.NotFound => NotFound(),
            SavingsAccountWriteOutcome.InvalidTransition => Problem(title: "Only an approved account can be closed.", statusCode: StatusCodes.Status400BadRequest),
            SavingsAccountWriteOutcome.NonZeroBalance => Problem(title: "Withdraw the full balance before closing.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/withdraw-account")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> WithdrawAccount(int id, [FromBody] string? notes, CancellationToken cancellationToken)
    {
        var result = await _accountService.WithdrawAccountAsync(id, notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsAccountWriteOutcome.Success => Ok(ToResponse(result.Account!)),
            SavingsAccountWriteOutcome.NotFound => NotFound(),
            SavingsAccountWriteOutcome.InvalidTransition => Problem(title: "Only an approved account can be withdrawn.", statusCode: StatusCodes.Status400BadRequest),
            SavingsAccountWriteOutcome.NonZeroBalance => Problem(title: "Withdraw the full balance first.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static SavingsAccountResponse ToResponse(SavingsAccount s) => new(
        s.Id, s.ClientType, s.ClientId, s.GroupId, s.OfficeId, s.SavingsProductId, s.AccountNumber, s.CurrencyId,
        s.InterestRate, s.AllowOverdraft, s.MinimumBalance, s.OverdraftLimit, s.Status, s.Balance, s.Deposits, s.Withdrawals,
        s.InterestEarned, s.InterestPosted, s.NextInterestCalculationDate, s.NextInterestPostingDate, s.Notes);
}
