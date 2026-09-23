using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Ownership-scoped CRUD under a client, no service class — matches the existing
/// "plain reads go straight through BCKashDbContext" precedent (OfficesController.List/Get),
/// extended to writes since there's no state machine or cross-entity invariant here to guard.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/identifications")]
[Authorize]
public class ClientIdentificationsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;

    public ClientIdentificationsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientIdentificationResponse>>> List(int clientId, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.ClientIdentifications.Where(ci => ci.ClientId == clientId).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientIdentificationResponse>> Get(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientIdentifications.FirstOrDefaultAsync(ci => ci.Id == id && ci.ClientId == clientId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientIdentificationResponse>> Create(int clientId, SaveClientIdentificationRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var item = new ClientIdentification
        {
            ClientId = clientId,
            ClientIdentificationTypeId = request.ClientIdentificationTypeId,
            Name = request.Name,
            Active = request.Active,
            Notes = request.Notes,
        };
        _db.ClientIdentifications.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { clientId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int clientId, int id, SaveClientIdentificationRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.ClientIdentifications.FirstOrDefaultAsync(ci => ci.Id == id && ci.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.ClientIdentificationTypeId = request.ClientIdentificationTypeId;
        item.Name = request.Name;
        item.Active = request.Active;
        item.Notes = request.Notes;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientIdentifications.FirstOrDefaultAsync(ci => ci.Id == id && ci.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.ClientIdentifications.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ClientIdentificationResponse ToResponse(ClientIdentification ci) =>
        new(ci.Id, ci.ClientId, ci.ClientIdentificationTypeId, ci.Name, ci.Active, ci.Notes);
}
