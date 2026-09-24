using BCKash.Api.Contracts;
using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private const string ManagePolicy = "Permission:expenses.manage";
    private const string ApprovePolicy = "Permission:expenses.approve";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IExpenseService _expenseService;

    public ExpensesController(BCKashDbContext db, IExpenseService expenseService)
    {
        _db = db;
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseResponse>>> List(
        [FromQuery] ApprovalStatus? status,
        [FromQuery] int? officeId,
        [FromQuery] int? expenseTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Expenses.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status);
        }

        if (officeId.HasValue)
        {
            query = query.Where(e => e.OfficeId == officeId);
        }

        if (expenseTypeId.HasValue)
        {
            query = query.Where(e => e.ExpenseTypeId == expenseTypeId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var expenses = await query.OrderByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<ExpenseResponse>(expenses.Select(ToResponse).ToList(), page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.FindAsync([id], cancellationToken);
        return expense is null ? NotFound() : Ok(ToResponse(expense));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(SaveExpenseRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.CreateAsync(ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            ExpenseWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Expense!.Id }, ToResponse(result.Expense)),
            ExpenseWriteOutcome.TypeNotFound => Problem(title: "Expense type not found.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveExpenseRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return ToResult(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _expenseService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ExpenseWriteOutcome.Success => NoContent(),
            ExpenseWriteOutcome.NotFound => NotFound(),
            ExpenseWriteOutcome.InvalidTransition => Problem(title: "Only a pending expense can be deleted.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Approve(int id, ApproveRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.ApproveAsync(id, request.Notes, cancellationToken);
        return ToResult(result);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ApprovePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToResult(result);
    }

    /// <summary>FR-EXP-1 — admin-triggered since no scheduler exists in this codebase.</summary>
    [HttpPost("run-recurring")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RunRecurring(CancellationToken cancellationToken)
    {
        var count = await _expenseService.GenerateDueRecurringAsync(cancellationToken);
        return Ok(new { generated = count });
    }

    private IActionResult ToResult(ExpenseWriteResult result) => result.Outcome switch
    {
        ExpenseWriteOutcome.Success => Ok(ToResponse(result.Expense!)),
        ExpenseWriteOutcome.NotFound => NotFound(),
        ExpenseWriteOutcome.TypeNotFound => Problem(title: "Expense type not found.", statusCode: StatusCodes.Status400BadRequest),
        ExpenseWriteOutcome.InvalidTransition => Problem(title: "This expense is no longer pending.", statusCode: StatusCodes.Status400BadRequest),
        ExpenseWriteOutcome.ReasonRequired => Problem(title: "A reason is required to decline an expense.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static Expense ToEntity(SaveExpenseRequest request) => new()
    {
        OfficeId = request.OfficeId,
        ExpenseTypeId = request.ExpenseTypeId,
        Name = request.Name,
        Amount = request.Amount,
        Date = request.Date,
        Year = request.Date?.Year.ToString(),
        Month = request.Date?.Month.ToString(),
        Recurring = request.Recurring,
        RecurFrequency = request.RecurFrequency ?? "1",
        RecurStartDate = request.RecurStartDate,
        RecurEndDate = request.RecurEndDate,
        RecurType = request.RecurType,
        Notes = request.Notes,
        Files = request.Files,
    };

    private static ExpenseResponse ToResponse(Expense e) => new(
        e.Id, e.OfficeId, e.ExpenseTypeId, e.Name, e.Amount, e.Date, e.Recurring, e.RecurFrequency,
        e.RecurStartDate, e.RecurEndDate, e.RecurNextDate, e.RecurType, e.Status,
        e.ApprovedById, e.ApprovedDate, e.DeclinedById, e.DeclinedDate, e.Notes);
}
