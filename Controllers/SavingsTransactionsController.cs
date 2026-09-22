using BCKash.Api.Contracts;
using BCKash.Application.Savings;
using BCKash.Domain.Savings;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/savings-accounts/{accountId:int}/transactions")]
[Authorize]
public class SavingsTransactionsController : ControllerBase
{
    private const string ManagePolicy = "Permission:savings-accounts.manage";

    private readonly BCKashDbContext _db;
    private readonly ISavingsTransactionService _transactionService;

    public SavingsTransactionsController(BCKashDbContext db, ISavingsTransactionService transactionService)
    {
        _db = db;
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavingsTransactionResponse>>> List(int accountId, CancellationToken cancellationToken)
    {
        var transactions = await _db.SavingsTransactions
            .Where(t => t.SavingsId == accountId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
        return Ok(transactions.Select(ToResponse).ToList());
    }

    [HttpPost("deposit")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SavingsTransactionResponse>> Deposit(int accountId, RecordSavingsTransactionRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactionService.RecordDepositAsync(accountId, request.Amount, request.Date, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsTransactionWriteOutcome.Success => CreatedAtAction(nameof(List), new { accountId }, ToResponse(result.Transaction!)),
            SavingsTransactionWriteOutcome.NotFound => NotFound(),
            SavingsTransactionWriteOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransactionWriteOutcome.InvalidAccountStatus => Problem(title: "Only an approved account can transact.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>FR-SAV-3: rejected (409) below the account's floor unless overdraft covers it.</summary>
    [HttpPost("withdrawal")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<SavingsTransactionResponse>> Withdrawal(int accountId, RecordSavingsTransactionRequest request, CancellationToken cancellationToken)
    {
        var result = await _transactionService.RecordWithdrawalAsync(accountId, request.Amount, request.Date, request.Notes, cancellationToken);
        return result.Outcome switch
        {
            SavingsTransactionWriteOutcome.Success => CreatedAtAction(nameof(List), new { accountId }, ToResponse(result.Transaction!)),
            SavingsTransactionWriteOutcome.NotFound => NotFound(),
            SavingsTransactionWriteOutcome.InvalidAmount => Problem(title: "Amount must be positive.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransactionWriteOutcome.InvalidAccountStatus => Problem(title: "Only an approved account can transact.", statusCode: StatusCodes.Status400BadRequest),
            SavingsTransactionWriteOutcome.InsufficientBalance => Problem(
                title: "This withdrawal would take the balance below the account's minimum balance (or overdraft limit).",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{transactionId:int}/reverse")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Reverse(int accountId, int transactionId, CancellationToken cancellationToken)
    {
        var result = await _transactionService.ReverseAsync(transactionId, cancellationToken);
        return result.Outcome switch
        {
            SavingsTransactionWriteOutcome.Success => Ok(ToResponse(result.Transaction!)),
            SavingsTransactionWriteOutcome.TransactionNotFound => NotFound(),
            SavingsTransactionWriteOutcome.AlreadyReversed => Problem(title: "This transaction has already been reversed.", statusCode: StatusCodes.Status409Conflict),
            SavingsTransactionWriteOutcome.NotReversible => Problem(title: "This transaction cannot be reversed.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static SavingsTransactionResponse ToResponse(SavingsTransaction t) => new(
        t.Id, t.SavingsId, t.TransactionType, t.Amount, t.Debit, t.Credit, t.Balance, t.Reversible, t.Reversed, t.Date, t.Notes);
}
