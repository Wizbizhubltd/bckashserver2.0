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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OfficeResponse>>> List(CancellationToken cancellationToken)
    {
        // Materialize first, then map — ToResponse isn't translatable to SQL by EF Core.
        var offices = await _db.Offices.ToListAsync(cancellationToken);
        return Ok(offices.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OfficeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var office = await _db.Offices.FindAsync([id], cancellationToken);
        return office is null ? NotFound() : Ok(ToResponse(office));
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
        };

        var result = await _officeService.CreateAsync(office, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Office!.Id }, ToResponse(result.Office));
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
        };

        var result = await _officeService.UpdateAsync(id, updated, cancellationToken);

        return result.Outcome switch
        {
            OfficeWriteOutcome.Success => Ok(ToResponse(result.Office!)),
            OfficeWriteOutcome.NotFound => NotFound(),
            OfficeWriteOutcome.CircularParent => Problem(
                title: "The selected parent office would create a circular office hierarchy.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
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
            OfficeWriteOutcome.Success => Ok(ToResponse(result.Office!)),
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
        return result.Outcome == OfficeWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Office!));
    }

    private static OfficeResponse ToResponse(Office o) => new(
        o.Id, o.Name, o.ParentId, o.ExternalId, o.OpeningDate, o.Address, o.Phone, o.Email, o.Notes,
        o.ManagerId, o.Active, o.DefaultOffice);
}
