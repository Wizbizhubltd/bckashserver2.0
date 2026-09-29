using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Application.Identity;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>Zones group offices. Any signed-in user can read them (for dropdowns and filters); only super admins can change them.</summary>
[ApiController]
[Route("api/v1/zones")]
[Authorize]
public class ZonesController : ControllerBase
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOfficeScope _scope;

    public ZonesController(BCKashDbContext db, ICurrentUserContext currentUser, IOfficeScope scope)
    {
        _scope = scope;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Every zone for a super admin; a director's own zones; for anyone else, the zone their office is in.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ZoneResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await ToResponsesAsync(await ScopedAsync(cancellationToken), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ZoneResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var zone = (await ToResponsesAsync((await ScopedAsync(cancellationToken)).Where(z => z.Id == id), cancellationToken)).SingleOrDefault();
        return zone is null ? NotFound() : Ok(zone);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<ZoneResponse>> Create(SaveZoneRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Problem(title: "Zone name is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (await _db.Zones.AnyAsync(z => z.Name == name, cancellationToken))
        {
            return Problem(title: "A zone with this name already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        var zone = new Zone
        {
            Name = name,
            Description = request.Description,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Zones.Add(zone);
        await _db.SaveChangesAsync(cancellationToken);

        var created = (await ToResponsesAsync(_db.Zones.Where(z => z.Id == zone.Id), cancellationToken)).Single();
        return CreatedAtAction(nameof(Get), new { id = zone.Id }, created);
    }

    /// <summary>
    /// Bulk action: moves every listed office into this zone (out of whatever zone it was in). The
    /// directors of both zones gain or lose those offices straight away.
    /// </summary>
    [HttpPost("{id:int}/offices")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<ZoneResponse>> AssignOffices(int id, AssignOfficesToZoneRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Zones.AnyAsync(z => z.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var officeIds = (request.OfficeIds ?? []).Distinct().ToList();
        if (officeIds.Count == 0)
        {
            return Problem(title: "Select at least one office.", statusCode: StatusCodes.Status400BadRequest);
        }

        var offices = await _db.Offices.Where(o => officeIds.Contains(o.Id)).ToListAsync(cancellationToken);
        if (offices.Count != officeIds.Count)
        {
            return Problem(title: "One or more offices were not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var office in offices)
        {
            office.ZoneId = id;
            office.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok((await ToResponsesAsync(_db.Zones.Where(z => z.Id == id), cancellationToken)).Single());
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<ActionResult<ZoneResponse>> Update(int id, SaveZoneRequest request, CancellationToken cancellationToken)
    {
        var zone = await _db.Zones.FindAsync([id], cancellationToken);
        if (zone is null)
        {
            return NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Problem(title: "Zone name is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (await _db.Zones.AnyAsync(z => z.Name == name && z.Id != id, cancellationToken))
        {
            return Problem(title: "A zone with this name already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        zone.Name = name;
        zone.Description = request.Description;
        zone.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok((await ToResponsesAsync(_db.Zones.Where(z => z.Id == id), cancellationToken)).Single());
    }

    /// <summary>Only allowed while none of the zone's offices has staff. Offices in the zone (all without staff) are left with no zone.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var zone = await _db.Zones.FindAsync([id], cancellationToken);
        if (zone is null)
        {
            return NotFound();
        }

        var offices = await _db.Offices.IgnoreQueryFilters().Where(o => o.ZoneId == id).ToListAsync(cancellationToken);
        var officeIds = offices.Select(o => o.Id).ToList();
        var staffCount = await _db.Users.CountAsync(u => u.OfficeId.HasValue && officeIds.Contains(u.OfficeId.Value), cancellationToken);
        if (staffCount > 0)
        {
            return Problem(
                title: $"This zone can't be deleted: {staffCount} staff are assigned to its offices.",
                statusCode: StatusCodes.Status409Conflict);
        }

        foreach (var office in offices)
        {
            office.ZoneId = null;
        }

        _db.Zones.Remove(zone);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<ZoneResponse>> ToResponsesAsync(IQueryable<Zone> query, CancellationToken cancellationToken)
    {
        var zones = await query.OrderByDescending(z => z.Id).ToListAsync(cancellationToken);
        var zoneIds = zones.Select(z => z.Id).ToList();

        var offices = await _db.Offices
            .Where(o => o.ZoneId.HasValue && zoneIds.Contains(o.ZoneId.Value))
            .Select(o => new { o.Id, ZoneId = o.ZoneId!.Value })
            .ToListAsync(cancellationToken);
        var officeIds = offices.Select(o => o.Id).ToList();
        var staffPerOffice = await _db.Users
            .Where(u => u.OfficeId.HasValue && officeIds.Contains(u.OfficeId.Value))
            .GroupBy(u => u.OfficeId!.Value)
            .Select(g => new { OfficeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OfficeId, x => x.Count, cancellationToken);

        var creatorIds = zones.Where(z => z.CreatedById.HasValue).Select(z => z.CreatedById!.Value).Distinct().ToList();
        var creatorNames = await _db.Users
            .Where(u => creatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(),
                cancellationToken);

        return zones.Select(z =>
        {
            var zoneOffices = offices.Where(o => o.ZoneId == z.Id).ToList();
            return new ZoneResponse(
                z.Id, z.Name, z.Description,
                zoneOffices.Count,
                zoneOffices.Sum(o => staffPerOffice.GetValueOrDefault(o.Id)),
                z.CreatedAt,
                z.CreatedById,
                z.CreatedById.HasValue ? creatorNames.GetValueOrDefault(z.CreatedById.Value) : null);
        }).ToList();
    }

    private async Task<IQueryable<Zone>> ScopedAsync(CancellationToken cancellationToken)
    {
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        if (officeIds is null)
        {
            return _db.Zones;
        }

        // A director's zones are the ones assigned to them, even while a zone has no offices yet.
        if (await _scope.GetUserTypeAsync(cancellationToken) == Domain.Identity.UserTypeSlugs.Director)
        {
            var userId = _currentUser.UserId;
            return _db.Zones.Where(z => _db.UserZones.Any(uz => uz.UserId == userId && uz.ZoneId == z.Id));
        }

        return _db.Zones.Where(z => _db.Offices.Any(o => o.ZoneId == z.Id && officeIds.Contains(o.Id)));
    }
}
