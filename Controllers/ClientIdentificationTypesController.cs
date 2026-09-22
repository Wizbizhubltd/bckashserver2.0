using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/client-identification-types")]
[Authorize]
public class ClientIdentificationTypesController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;

    public ClientIdentificationTypesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientIdentificationTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.ClientIdentificationTypes.ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientIdentificationTypeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientIdentificationTypes.FindAsync([id], cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientIdentificationTypeResponse>> Create(SaveClientIdentificationTypeRequest request, CancellationToken cancellationToken)
    {
        var item = new ClientIdentificationType { Name = request.Name };
        _db.ClientIdentificationTypes.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveClientIdentificationTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.ClientIdentificationTypes.FindAsync([id], cancellationToken);
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
        var item = await _db.ClientIdentificationTypes.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.ClientIdentificationTypes.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ClientIdentificationTypeResponse ToResponse(ClientIdentificationType t) => new(t.Id, t.Name);
}
