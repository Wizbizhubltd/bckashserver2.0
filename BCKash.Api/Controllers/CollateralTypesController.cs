using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/collateral-types")]
[Authorize]
public class CollateralTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-products.manage";

    private readonly BCKashDbContext _db;

    public CollateralTypesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CollateralTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.CollateralTypes.ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CollateralTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _db.CollateralTypes.FindAsync([id], cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<CollateralTypeResponse>> Create(SaveCollateralTypeRequest request, CancellationToken cancellationToken)
    {
        var item = new CollateralType { Name = request.Name };
        _db.CollateralTypes.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveCollateralTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.CollateralTypes.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _db.CollateralTypes.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.CollateralTypes.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static CollateralTypeResponse ToResponse(CollateralType t) => new(t.Id, t.Name);
}
