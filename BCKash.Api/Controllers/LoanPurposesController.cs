using BCKash.Api.Contracts;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/loan-purposes")]
[Authorize]
public class LoanPurposesController : ControllerBase
{
    private const string ManagePolicy = "Permission:loan-products.manage";

    private readonly BCKashDbContext _db;

    public LoanPurposesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanPurposeResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.LoanPurposes.ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanPurposeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _db.LoanPurposes.FindAsync([id], cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<LoanPurposeResponse>> Create(SaveLoanPurposeRequest request, CancellationToken cancellationToken)
    {
        var item = new LoanPurpose { Name = request.Name };
        _db.LoanPurposes.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveLoanPurposeRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.LoanPurposes.FindAsync([id], cancellationToken);
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
        var item = await _db.LoanPurposes.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.LoanPurposes.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static LoanPurposeResponse ToResponse(LoanPurpose p) => new(p.Id, p.Name);
}
