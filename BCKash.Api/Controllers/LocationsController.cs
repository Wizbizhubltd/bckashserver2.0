using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// State → LGA → city lookups for office addresses. States and LGAs are fixed, seeded reference
/// data; cities are added by super admins.
/// </summary>
[ApiController]
[Route("api/v1/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public LocationsController(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("states")]
    public async Task<ActionResult<IReadOnlyCollection<StateResponse>>> States(CancellationToken cancellationToken) =>
        Ok(await _db.States.OrderBy(s => s.Name).Select(s => new StateResponse(s.Id, s.Name)).ToListAsync(cancellationToken));

    [HttpGet("lgas")]
    public async Task<ActionResult<IReadOnlyCollection<LgaResponse>>> Lgas([FromQuery] int? stateId, CancellationToken cancellationToken)
    {
        var query = _db.Lgas.AsQueryable();
        if (stateId.HasValue) query = query.Where(l => l.StateId == stateId);

        return Ok(await query.OrderBy(l => l.Name).Select(l => new LgaResponse(l.Id, l.StateId, l.Name)).ToListAsync(cancellationToken));
    }

    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyCollection<CityResponse>>> Cities([FromQuery] int? stateId, [FromQuery] int? lgaId, CancellationToken cancellationToken)
    {
        var query = _db.Cities.AsQueryable();
        if (stateId.HasValue) query = query.Where(c => c.Lga.StateId == stateId);
        if (lgaId.HasValue) query = query.Where(c => c.LgaId == lgaId);

        return Ok(await ToResponsesAsync(query, cancellationToken));
    }

    [HttpPost("cities")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<CityResponse>> CreateCity(SaveCityRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Problem(title: "City name is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.Lgas.AnyAsync(l => l.Id == request.LgaId, cancellationToken))
        {
            return Problem(title: "LGA not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (await _db.Cities.AnyAsync(c => c.LgaId == request.LgaId && c.Name == name, cancellationToken))
        {
            return Problem(title: "This city already exists in the selected LGA.", statusCode: StatusCodes.Status409Conflict);
        }

        var city = new City
        {
            LgaId = request.LgaId,
            Name = name,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Cities.Add(city);
        await _db.SaveChangesAsync(cancellationToken);

        var created = (await ToResponsesAsync(_db.Cities.Where(c => c.Id == city.Id), cancellationToken)).Single();
        return Created($"/api/v1/locations/cities/{city.Id}", created);
    }

    [HttpPut("cities/{id:int}")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<CityResponse>> UpdateCity(int id, SaveCityRequest request, CancellationToken cancellationToken)
    {
        var city = await _db.Cities.FindAsync([id], cancellationToken);
        if (city is null)
        {
            return NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Problem(title: "City name is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Moving a city to another LGA would silently put the offices using it in the wrong LGA.
        if (request.LgaId != city.LgaId && await _db.Offices.IgnoreQueryFilters().AnyAsync(o => o.CityId == id, cancellationToken))
        {
            return Problem(title: "This city is used by offices, so its LGA can't be changed.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await _db.Lgas.AnyAsync(l => l.Id == request.LgaId, cancellationToken))
        {
            return Problem(title: "LGA not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (await _db.Cities.AnyAsync(c => c.LgaId == request.LgaId && c.Name == name && c.Id != id, cancellationToken))
        {
            return Problem(title: "This city already exists in the selected LGA.", statusCode: StatusCodes.Status409Conflict);
        }

        city.LgaId = request.LgaId;
        city.Name = name;
        city.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok((await ToResponsesAsync(_db.Cities.Where(c => c.Id == id), cancellationToken)).Single());
    }

    [HttpDelete("cities/{id:int}")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> DeleteCity(int id, CancellationToken cancellationToken)
    {
        var city = await _db.Cities.FindAsync([id], cancellationToken);
        if (city is null)
        {
            return NotFound();
        }

        if (await _db.Offices.IgnoreQueryFilters().AnyAsync(o => o.CityId == id, cancellationToken))
        {
            return Problem(title: "This city is used by offices and can't be deleted.", statusCode: StatusCodes.Status409Conflict);
        }

        _db.Cities.Remove(city);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<CityResponse>> ToResponsesAsync(IQueryable<City> query, CancellationToken cancellationToken)
    {
        var cities = await query
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.LgaId, LgaName = c.Lga.Name, c.Lga.StateId, StateName = c.Lga.State.Name })
            .ToListAsync(cancellationToken);

        var cityIds = cities.Select(c => c.Id).ToList();
        var officeCounts = await _db.Offices
            .Where(o => o.CityId.HasValue && cityIds.Contains(o.CityId.Value))
            .GroupBy(o => o.CityId!.Value)
            .Select(g => new { CityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CityId, x => x.Count, cancellationToken);

        return cities
            .Select(c => new CityResponse(c.Id, c.Name, c.LgaId, c.LgaName, c.StateId, c.StateName, officeCounts.GetValueOrDefault(c.Id)))
            .ToList();
    }
}
