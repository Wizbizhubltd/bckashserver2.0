using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using BCKash.Application.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// A client's loan savings (see IClientSavingsService): anyone who can see the client sees the balance;
/// staff who service loans pay out a withdrawal.
/// </summary>
[ApiController]
[Route("api/v1/clients/{clientId:int}/savings")]
[Authorize]
public class ClientSavingsController : ControllerBase
{
    private const string ServicingPermission = "loan-servicing.manage";

    private readonly BCKashDbContext _db;
    private readonly IClientAccess _access;
    private readonly IClientSavingsService _savings;

    public ClientSavingsController(BCKashDbContext db, IClientAccess access, IClientSavingsService savings)
    {
        _db = db;
        _access = access;
        _savings = savings;
    }

    [HttpGet]
    public async Task<ActionResult<ClientSavingsResponse>> Get(int clientId, CancellationToken cancellationToken)
    {
        if (await _access.FindVisibleAsync(clientId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await ToResponseAsync(await _savings.GetAsync(clientId, cancellationToken), cancellationToken));
    }

    /// <summary>Pays out the whole balance — less 15% while one of the client's loans is still running.</summary>
    [HttpPost("withdraw")]
    [Authorize(Policy = "Permission:" + ServicingPermission)]
    public async Task<ActionResult<SavingsWithdrawalResponse>> Withdraw(int clientId, WithdrawSavingsRequest? request, CancellationToken cancellationToken)
    {
        if (await _access.FindVisibleAsync(clientId, cancellationToken) is null)
        {
            return NotFound();
        }

        var result = await _savings.WithdrawAsync(clientId, request?.Notes, cancellationToken);
        return result.Outcome switch
        {
            ClientSavingsWithdrawalOutcome.Success => Ok(new SavingsWithdrawalResponse(result.Payout, result.Fee, await ToResponseAsync(result.Summary!, cancellationToken))),
            ClientSavingsWithdrawalOutcome.NothingToWithdraw => Problem(title: "There are no savings to withdraw.", statusCode: StatusCodes.Status400BadRequest),
            _ => NotFound(),
        };
    }

    private async Task<ClientSavingsResponse> ToResponseAsync(ClientSavingsSummary summary, CancellationToken cancellationToken)
    {
        var loanIds = summary.Entries.Where(e => e.LoanId.HasValue).Select(e => e.LoanId!.Value).Distinct().ToList();
        var loanNumbers = await _db.Loans.Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.AccountNumber, cancellationToken);
        var userIds = summary.Entries.Where(e => e.CreatedById.HasValue).Select(e => e.CreatedById!.Value).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } name ? name : u.Email, cancellationToken);

        var entries = summary.Entries.Select(e => new ClientSavingsEntryResponse(
            e.Id, e.Type, e.Amount, e.LoanId,
            e.LoanId.HasValue ? loanNumbers.GetValueOrDefault(e.LoanId.Value) : null,
            e.Notes, e.CreatedAt,
            e.CreatedById.HasValue ? names.GetValueOrDefault(e.CreatedById.Value) : null)).ToList();

        return new ClientSavingsResponse(
            summary.Balance, summary.HasRunningLoan, summary.EarlyWithdrawalFeeRate, summary.WithdrawalFee, summary.WithdrawalPayout,
            CanWithdraw: User.HasClaim(AuthClaimTypes.Permission, ServicingPermission),
            entries);
    }
}
