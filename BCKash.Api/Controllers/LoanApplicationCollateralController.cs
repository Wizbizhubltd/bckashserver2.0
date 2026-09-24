using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>Collateral captured at the application stage (FR-LN-22, via the additive `Collateral.LoanApplicationId` column — see Collateral.cs).</summary>
[ApiController]
[Route("api/v1/loan-applications/{applicationId:int}/collateral")]
[Authorize]
public class LoanApplicationCollateralController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-applications.manage";

    private readonly BCKashDbContext _db;

    public LoanApplicationCollateralController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CollateralResponse>>> List(int applicationId, CancellationToken cancellationToken)
    {
        if (!await _db.LoanApplications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.Collateral.Where(c => c.LoanApplicationId == applicationId).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CollateralResponse>> Get(int applicationId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.Collateral.FirstOrDefaultAsync(c => c.Id == id && c.LoanApplicationId == applicationId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<CollateralResponse>> Create(int applicationId, SaveCollateralRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.LoanApplications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            return NotFound();
        }

        var item = new Collateral
        {
            LoanApplicationId = applicationId,
            ClientId = request.ClientId,
            CollateralTypeId = request.CollateralTypeId,
            Name = request.Name,
            Serial = request.Serial,
            Value = request.Value,
            Description = request.Description,
        };
        _db.Collateral.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { applicationId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int applicationId, int id, SaveCollateralRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.Collateral.FirstOrDefaultAsync(c => c.Id == id && c.LoanApplicationId == applicationId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.ClientId = request.ClientId;
        item.CollateralTypeId = request.CollateralTypeId;
        item.Name = request.Name;
        item.Serial = request.Serial;
        item.Value = request.Value;
        item.Description = request.Description;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int applicationId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.Collateral.FirstOrDefaultAsync(c => c.Id == id && c.LoanApplicationId == applicationId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.Collateral.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static CollateralResponse ToResponse(Collateral c) =>
        new(c.Id, c.LoanId, c.LoanApplicationId, c.ClientId, c.CollateralTypeId, c.Name, c.Serial, c.Value, c.Description);
}
