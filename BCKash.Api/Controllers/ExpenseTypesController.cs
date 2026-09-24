using BCKash.Api.Contracts;
using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/expense-types")]
[Authorize]
public class ExpenseTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:expenses.manage";

    private readonly BCKashDbContext _db;
    private readonly IExpenseTypeService _expenseTypeService;

    public ExpenseTypesController(BCKashDbContext db, IExpenseTypeService expenseTypeService)
    {
        _db = db;
        _expenseTypeService = expenseTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var types = await _db.ExpenseTypes.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Ok(types.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var type = await _db.ExpenseTypes.FindAsync([id], cancellationToken);
        return type is null ? NotFound() : Ok(ToResponse(type));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ExpenseTypeResponse>> Create(SaveExpenseTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseTypeService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.ExpenseType!.Id }, ToResponse(result.ExpenseType));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveExpenseTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseTypeService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            ExpenseTypeWriteOutcome.Success => Ok(ToResponse(result.ExpenseType!)),
            ExpenseTypeWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _expenseTypeService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ExpenseTypeWriteOutcome.Success => NoContent(),
            ExpenseTypeWriteOutcome.NotFound => NotFound(),
            ExpenseTypeWriteOutcome.InUse => Problem(title: "This expense type is in use.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static ExpenseType ToEntity(SaveExpenseTypeRequest request) => new()
    {
        Name = request.Name,
        GlAccountAssetId = request.GlAccountAssetId,
        GlAccountExpenseId = request.GlAccountExpenseId,
        Notes = request.Notes,
    };

    private static ExpenseTypeResponse ToResponse(ExpenseType t) => new(t.Id, t.Name, t.GlAccountAssetId, t.GlAccountExpenseId, t.Notes);
}
