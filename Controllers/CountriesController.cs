using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/countries")]
[Authorize]
public class CountriesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public CountriesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CountryResponse>>> List(CancellationToken cancellationToken)
    {
        var countries = await _db.Countries.ToListAsync(cancellationToken);
        return Ok(countries.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CountryResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var country = await _db.Countries.FindAsync([id], cancellationToken);
        return country is null ? NotFound() : Ok(ToResponse(country));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<CountryResponse>> Create(SaveCountryRequest request, CancellationToken cancellationToken)
    {
        var country = new Country { Sortname = request.Sortname, Name = request.Name };
        _db.Countries.Add(country);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = country.Id }, ToResponse(country));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveCountryRequest request, CancellationToken cancellationToken)
    {
        var country = await _db.Countries.FindAsync([id], cancellationToken);
        if (country is null)
        {
            return NotFound();
        }

        country.Sortname = request.Sortname;
        country.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(country));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var country = await _db.Countries.FindAsync([id], cancellationToken);
        if (country is null)
        {
            return NotFound();
        }

        _db.Countries.Remove(country);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static CountryResponse ToResponse(Country c) => new(c.Id, c.Sortname, c.Name);
}
