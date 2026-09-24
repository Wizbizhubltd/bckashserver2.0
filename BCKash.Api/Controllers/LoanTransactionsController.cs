using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Cross-loan transaction search — backs the dashboard's "Disbursements" and "Repayments" tiles,
/// which need to browse transactions network-wide rather than one loan at a time (that's
/// LoanRepaymentsController's job, scoped to a single loan).
/// </summary>
[ApiController]
[Route("api/v1/loan-transactions")]
[Authorize]
public class LoanTransactionsController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;

    public LoanTransactionsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LoanTransactionResponse>>> List(
        [FromQuery] LoanTransactionType? transactionType,
        [FromQuery] int? officeId,
        [FromQuery] int? loanId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool includeReversed,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.LoanTransactions.AsQueryable();

        if (transactionType.HasValue)
        {
            query = query.Where(t => t.TransactionType == transactionType);
        }

        if (officeId.HasValue)
        {
            query = query.Where(t => t.OfficeId == officeId);
        }

        if (loanId.HasValue)
        {
            query = query.Where(t => t.LoanId == loanId);
        }

        if (from.HasValue)
        {
            query = query.Where(t => t.Date >= from);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.Date <= to);
        }

        if (!includeReversed)
        {
            query = query.Where(t => !t.Reversed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                (t.Receipt != null && t.Receipt.Contains(search)) ||
                (t.Loan != null && t.Loan.AccountNumber != null && t.Loan.AccountNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = transactions.Select(ToResponse).ToList();
        return Ok(new PagedResult<LoanTransactionResponse>(items, page, pageSize, totalCount));
    }

    private static LoanTransactionResponse ToResponse(LoanTransaction t) =>
        new(t.Id, t.LoanId, t.TransactionType, t.Amount, t.Principal, t.Interest, t.Fee, t.Penalty, t.Overpayment, t.Date, t.Reversible, t.Reversed, t.Notes);
}
