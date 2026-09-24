using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/client-professions")]
[Authorize]
public class ClientProfessionsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;

    public ClientProfessionsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientProfessionResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.ClientProfessions.ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientProfessionResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientProfessions.FindAsync([id], cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientProfessionResponse>> Create(SaveClientProfessionRequest request, CancellationToken cancellationToken)
    {
        var item = new ClientProfession { Name = request.Name };
        _db.ClientProfessions.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveClientProfessionRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.ClientProfessions.FindAsync([id], cancellationToken);
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
        var item = await _db.ClientProfessions.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.ClientProfessions.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ClientProfessionResponse ToResponse(ClientProfession p) => new(p.Id, p.Name);
}
