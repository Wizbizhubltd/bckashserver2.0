using BCKash.Api.Contracts;
using BCKash.Application.Expenses;
using BCKash.Domain.Expenses;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/other-income-types")]
[Authorize]
public class OtherIncomeTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:other-income.manage";

    private readonly BCKashDbContext _db;
    private readonly IOtherIncomeTypeService _incomeTypeService;

    public OtherIncomeTypesController(BCKashDbContext db, IOtherIncomeTypeService incomeTypeService)
    {
        _db = db;
        _incomeTypeService = incomeTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OtherIncomeTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var types = await _db.OtherIncomeTypes.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Ok(types.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OtherIncomeTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var type = await _db.OtherIncomeTypes.FindAsync([id], cancellationToken);
        return type is null ? NotFound() : Ok(ToResponse(type));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<OtherIncomeTypeResponse>> Create(SaveOtherIncomeTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeTypeService.CreateAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.OtherIncomeType!.Id }, ToResponse(result.OtherIncomeType));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveOtherIncomeTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _incomeTypeService.UpdateAsync(id, ToEntity(request), cancellationToken);
        return result.Outcome switch
        {
            OtherIncomeTypeWriteOutcome.Success => Ok(ToResponse(result.OtherIncomeType!)),
            OtherIncomeTypeWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _incomeTypeService.DeleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            OtherIncomeTypeWriteOutcome.Success => NoContent(),
            OtherIncomeTypeWriteOutcome.NotFound => NotFound(),
            OtherIncomeTypeWriteOutcome.InUse => Problem(title: "This income type is in use.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static OtherIncomeType ToEntity(SaveOtherIncomeTypeRequest request) => new()
    {
        Name = request.Name,
        GlAccountAssetId = request.GlAccountAssetId,
        GlAccountIncomeId = request.GlAccountIncomeId,
        Notes = request.Notes,
    };

    private static OtherIncomeTypeResponse ToResponse(OtherIncomeType t) => new(t.Id, t.Name, t.GlAccountAssetId, t.GlAccountIncomeId, t.Notes);
}
