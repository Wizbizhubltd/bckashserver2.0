using BCKash.Api.Contracts;
using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/expense-budgets")]
[Authorize]
public class ExpenseBudgetsController : ControllerBase
{
    private const string ManagePolicy = "Permission:expense-budgets.manage";
    private const string ApprovePolicy = "Permission:expense-budgets.approve";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IExpenseBudgetService _budgetService;

    public ExpenseBudgetsController(BCKashDbContext db, IExpenseBudgetService budgetService)
    {
        _db = db;
        _budgetService = budgetService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseBudgetResponse>>> List(
        [FromQuery] ApprovalStatus? status,
        [FromQuery] int? officeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.ExpenseBudgets.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status);
        }

        if (officeId.HasValue)
        {
            query = query.Where(b => b.OfficeId == officeId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var budgets = await query.OrderByDescending(b => b.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<ExpenseBudgetResponse>(budgets.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseBudgetResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var budget = await _db.ExpenseBudgets.FindAsync([id], cancellationToken);
        return budget is null ? NotFound() : Ok(ToResponse(budget));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveExpenseBudgetRequest request, CancellationToken cancellationToken)
    {
        var result = await _budgetService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Budget!.Id }, ToResponse(result.Budget));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveExpenseBudgetRequest request, CancellationToken cancellationToken)
    {
        var result = await _budgetService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return ToResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _budgetService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ExpenseBudgetWriteOutcome.Success => NoContent(),
            ExpenseBudgetWriteOutcome.NotFound => NotFound(),
            ExpenseBudgetWriteOutcome.InvalidTransition => Problem(title: "Only a pending budget can be deleted.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var result = await _budgetService.ApproveAsync(id, cancellationToken);
        return ToResult(result);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _budgetService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToResult(result);
    }

    private IActionResult ToResult(ExpenseBudgetWriteResult result) => result.Outcome switch
    {
        ExpenseBudgetWriteOutcome.Success => Ok(ToResponse(result.Budget!)),
        ExpenseBudgetWriteOutcome.NotFound => NotFound(),
        ExpenseBudgetWriteOutcome.InvalidTransition => Problem(title: "This budget is no longer pending.", statusCode: StatusCodes.Status400BadRequest),
        ExpenseBudgetWriteOutcome.ReasonRequired => Problem(title: "A reason is required to decline a budget.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static ExpenseBudget ToEntity(SaveExpenseBudgetRequest request) => new()
    {
        OfficeId = request.OfficeId,
        ExpenseTypeId = request.ExpenseTypeId,
        Name = request.Name,
        Year = request.Year,
        Month = request.Month,
        Date = request.Date,
        Amount = request.Amount,
        Notes = request.Notes,
    };

    private static ExpenseBudgetResponse ToResponse(ExpenseBudget b) => new(
        b.Id, b.OfficeId, b.ExpenseTypeId, b.Name, b.Year, b.Month, b.Date, b.Amount, b.Notes,
        b.Status, b.ApprovedById, b.ApprovedDate, b.DeclinedById, b.DeclinedDate);
}
