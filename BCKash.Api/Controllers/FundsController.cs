using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/funds")]
[Authorize]
public class FundsController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public FundsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FundResponse>>> List(CancellationToken cancellationToken)
    {
        var funds = await _db.Funds.ToListAsync(cancellationToken);
        return Ok(funds.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FundResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var fund = await _db.Funds.FindAsync([id], cancellationToken);
        return fund is null ? NotFound() : Ok(ToResponse(fund));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<FundResponse>> Create(SaveFundRequest request, CancellationToken cancellationToken)
    {
        var fund = new Fund { Name = request.Name };
        _db.Funds.Add(fund);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = fund.Id }, ToResponse(fund));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveFundRequest request, CancellationToken cancellationToken)
    {
        var fund = await _db.Funds.FindAsync([id], cancellationToken);
        if (fund is null)
        {
            return NotFound();
        }

        fund.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(fund));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var fund = await _db.Funds.FindAsync([id], cancellationToken);
        if (fund is null)
        {
            return NotFound();
        }

        _db.Funds.Remove(fund);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static FundResponse ToResponse(Fund f) => new(f.Id, f.Name);
}
