using BCKash.Api.Contracts;
using BCKash.Application.Organization;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/offices")]
[Authorize]
public class OfficesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;
    private readonly IOfficeService _officeService;

    public OfficesController(BCKashDbContext db, IOfficeService officeService)
    {
        _db = db;
        _officeService = officeService;
    }

    /// <summary>All offices (unpaginated — it's reference-data sized), optionally filtered. Every filter is optional and they combine.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OfficeResponse>>> List(
        [FromQuery] OfficeTypeFilter? type,
        [FromQuery] int? stateId,
        [FromQuery] int? lgaId,
        [FromQuery] int? cityId,
        [FromQuery] int? zoneId,
        CancellationToken cancellationToken)
    {
        var query = _db.Offices.AsQueryable();
        if (type.HasValue) query = query.Where(o => o.DefaultOffice == (type == OfficeTypeFilter.Head));
        if (stateId.HasValue) query = query.Where(o => o.StateId == stateId);
        if (lgaId.HasValue) query = query.Where(o => o.LgaId == lgaId);
        if (cityId.HasValue) query = query.Where(o => o.CityId == cityId);
        if (zoneId.HasValue) query = query.Where(o => o.ZoneId == zoneId);

        return Ok(await ToResponsesAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OfficeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var office = (await ToResponsesAsync(_db.Offices.Where(o => o.Id == id), cancellationToken)).SingleOrDefault();
        return office is null ? NotFound() : Ok(office);
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<OfficeResponse>> Create(SaveOfficeRequest request, CancellationToken cancellationToken)
    {
        var office = new Office
        {
            Name = request.Name,
            ParentId = request.ParentId,
            ExternalId = request.ExternalId,
            OpeningDate = request.OpeningDate,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            Notes = request.Notes,
            ManagerId = request.ManagerId,
            DefaultOffice = request.DefaultOffice,
            StateId = request.StateId,
            LgaId = request.LgaId,
            CityId = request.CityId,
            ZoneId = request.ZoneId,
        };

        var result = await _officeService.CreateAsync(office, cancellationToken);
        if (result.Outcome != OfficeWriteOutcome.Success)
        {
            return ToProblem(result.Outcome);
        }

        var created = (await ToResponsesAsync(_db.Offices.Where(o => o.Id == result.Office!.Id), cancellationToken)).Single();
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveOfficeRequest request, CancellationToken cancellationToken)
    {
        var updated = new Office
        {
            Name = request.Name,
            ParentId = request.ParentId,
            ExternalId = request.ExternalId,
            OpeningDate = request.OpeningDate,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            Notes = request.Notes,
            ManagerId = request.ManagerId,
            DefaultOffice = request.DefaultOffice,
            StateId = request.StateId,
            LgaId = request.LgaId,
            CityId = request.CityId,
            ZoneId = request.ZoneId,
        };

        var result = await _officeService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome == OfficeWriteOutcome.Success
            ? await OkResponseAsync(result.Office!.Id, cancellationToken)
            : ToProblem(result.Outcome);
    }

    /// <summary>
    /// Deactivates an office. If it has active clients or open loans, returns 409 with
    /// counts unless <paramref name="confirm"/>=true — the SPA re-submits with
    /// confirm=true after the operator explicitly acknowledges the warning.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, [FromQuery] bool confirm, CancellationToken cancellationToken)
    {
        var result = await _officeService.DeactivateAsync(id, confirm, cancellationToken);

        return result.Outcome switch
        {
            OfficeWriteOutcome.Success => await OkResponseAsync(id, cancellationToken),
            OfficeWriteOutcome.NotFound => NotFound(),
            OfficeWriteOutcome.InUseConfirmationRequired => Conflict(
                new OfficeInUseResponse(result.InUse!.ActiveClientCount, result.InUse.OpenLoanCount)),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var result = await _officeService.ActivateAsync(id, cancellationToken);
        return result.Outcome == OfficeWriteOutcome.NotFound ? NotFound() : await OkResponseAsync(id, cancellationToken);
    }

    private ActionResult ToProblem(OfficeWriteOutcome outcome) => outcome switch
    {
        OfficeWriteOutcome.NotFound => NotFound(),
        OfficeWriteOutcome.CircularParent => Problem(
            title: "The selected parent office would create a circular office hierarchy.",
            statusCode: StatusCodes.Status400BadRequest),
        OfficeWriteOutcome.LocationRequired => Problem(title: "State, LGA, city and zone are required.", statusCode: StatusCodes.Status400BadRequest),
        OfficeWriteOutcome.InvalidLocation => Problem(
            title: "The selected LGA must be in the selected state, and the city in the selected LGA.",
            statusCode: StatusCodes.Status400BadRequest),
        OfficeWriteOutcome.ZoneNotFound => Problem(title: "Zone not found.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private async Task<IActionResult> OkResponseAsync(int id, CancellationToken cancellationToken) =>
        Ok((await ToResponsesAsync(_db.Offices.Where(o => o.Id == id), cancellationToken)).Single());

    private async Task<List<OfficeResponse>> ToResponsesAsync(IQueryable<Office> query, CancellationToken cancellationToken)
    {
        var offices = await query
            .Include(o => o.Parent)
            .Include(o => o.State)
            .Include(o => o.Lga)
            .Include(o => o.City)
            .Include(o => o.Zone)
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken);

        var officeIds = offices.Select(o => o.Id).ToList();
        var staffCounts = await _db.Users
            .Where(u => u.OfficeId.HasValue && officeIds.Contains(u.OfficeId.Value))
            .GroupBy(u => u.OfficeId!.Value)
            .Select(g => new { OfficeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OfficeId, x => x.Count, cancellationToken);

        var creatorIds = offices.Where(o => o.CreatedById.HasValue).Select(o => o.CreatedById!.Value).Distinct().ToList();
        var creatorNames = await _db.Users
            .Where(u => creatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(),
                cancellationToken);

        return offices.Select(o => new OfficeResponse(
            o.Id, o.Name, o.ParentId, o.ExternalId, o.OpeningDate, o.Address, o.Phone, o.Email, o.Notes,
            o.ManagerId, o.Active, o.DefaultOffice,
            o.OfficeCode, o.Parent?.Name,
            o.StateId, o.State?.Name, o.LgaId, o.Lga?.Name, o.CityId, o.City?.Name,
            o.ZoneId, o.Zone?.Name,
            staffCounts.GetValueOrDefault(o.Id),
            o.CreatedAt,
            o.CreatedById,
            o.CreatedById.HasValue ? creatorNames.GetValueOrDefault(o.CreatedById.Value) : null)).ToList();
    }
}
