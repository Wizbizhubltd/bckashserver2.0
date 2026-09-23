using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:int}/next-of-guardians")]
[Authorize]
public class ClientNextOfGuardiansController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;

    public ClientNextOfGuardiansController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientNextOfGuardianResponse>>> List(int clientId, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.ClientNextOfGuardians.Where(g => g.ClientId == clientId).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientNextOfGuardianResponse>> Get(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientNextOfGuardians.FirstOrDefaultAsync(g => g.Id == id && g.ClientId == clientId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientNextOfGuardianResponse>> Create(int clientId, SaveClientNextOfGuardianRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var item = new ClientNextOfGuardian
        {
            ClientId = clientId,
            ClientRelationshipId = request.ClientRelationshipId,
            Qualification = request.Qualification,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Ward = request.Ward,
            Street = request.Street,
            District = request.District,
            Region = request.Region,
            Address = request.Address,
            Mobile = request.Mobile,
            Phone = request.Phone,
            Email = request.Email,
            Gender = request.Gender,
            Notes = request.Notes,
        };
        _db.ClientNextOfGuardians.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { clientId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int clientId, int id, SaveClientNextOfGuardianRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.ClientNextOfGuardians.FirstOrDefaultAsync(g => g.Id == id && g.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.ClientRelationshipId = request.ClientRelationshipId;
        item.Qualification = request.Qualification;
        item.FirstName = request.FirstName;
        item.MiddleName = request.MiddleName;
        item.LastName = request.LastName;
        item.Ward = request.Ward;
        item.Street = request.Street;
        item.District = request.District;
        item.Region = request.Region;
        item.Address = request.Address;
        item.Mobile = request.Mobile;
        item.Phone = request.Phone;
        item.Email = request.Email;
        item.Gender = request.Gender;
        item.Notes = request.Notes;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientNextOfGuardians.FirstOrDefaultAsync(g => g.Id == id && g.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.ClientNextOfGuardians.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ClientNextOfGuardianResponse ToResponse(ClientNextOfGuardian g) => new(
        g.Id, g.ClientId, g.ClientRelationshipId, g.Qualification, g.FirstName, g.MiddleName, g.LastName,
        g.Ward, g.Street, g.District, g.Region, g.Address, g.Mobile, g.Phone, g.Email, g.Gender, g.Notes);
}
