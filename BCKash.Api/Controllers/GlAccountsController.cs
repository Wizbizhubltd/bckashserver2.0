using BCKash.Api.Contracts;
using BCKash.Application.GeneralLedger;
using BCKash.Domain.GeneralLedger;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/gl-accounts")]
[Authorize]
public class GlAccountsController : ControllerBase
{
    private const string ManagePolicy = "Permission:gl.manage";

    private readonly BCKashDbContext _db;
    private readonly IGlAccountService _accountService;

    public GlAccountsController(BCKashDbContext db, IGlAccountService accountService)
    {
        _db = db;
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GlAccountResponse>>> List(CancellationToken cancellationToken)
    {
        var accounts = await _db.GlAccounts.ToListAsync(cancellationToken);
        return Ok(accounts.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GlAccountResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var account = await _db.GlAccounts.FindAsync([id], cancellationToken);
        return account is null ? NotFound() : Ok(ToResponse(account));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<GlAccountResponse>> Create(SaveGlAccountRequest request, CancellationToken cancellationToken)
    {
        var account = new GlAccount
        {
            Name = request.Name,
            ParentId = request.ParentId,
            GlCode = request.GlCode,
            AccountType = request.AccountType,
            ManualEntries = request.ManualEntries,
            Notes = request.Notes,
        };

        var result = await _accountService.CreateAsync(account, cancellationToken);
        return result.Outcome switch
        {
            GlAccountWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Account!.Id }, ToResponse(result.Account)),
            GlAccountWriteOutcome.NotFound => Problem(title: "Parent GL account not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveGlAccountRequest request, CancellationToken cancellationToken)
    {
        var updated = new GlAccount
        {
            Name = request.Name,
            ParentId = request.ParentId,
            GlCode = request.GlCode,
            AccountType = request.AccountType,
            ManualEntries = request.ManualEntries,
            Notes = request.Notes,
        };

        var result = await _accountService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome switch
        {
            GlAccountWriteOutcome.Success => Ok(ToResponse(result.Account!)),
            GlAccountWriteOutcome.NotFound => NotFound(),
            GlAccountWriteOutcome.CircularParent => Problem(
                title: "The selected parent account would create a circular chart-of-accounts hierarchy.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeactivateAsync(id, cancellationToken);
        return result.Outcome == GlAccountWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Account!));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var result = await _accountService.ActivateAsync(id, cancellationToken);
        return result.Outcome == GlAccountWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Account!));
    }

    private static GlAccountResponse ToResponse(GlAccount a) =>
        new(a.Id, a.Name, a.ParentId, a.GlCode, a.AccountType, a.Active, a.ManualEntries, a.Notes);
}
