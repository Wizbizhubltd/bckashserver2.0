using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:int}/notes")]
[Authorize]
public class ClientNotesController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ClientNotesController(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NoteResponse>>> List(int clientId, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.Notes
            .Where(n => n.Type == ReferenceEntityType.Client && n.ReferenceId == clientId)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteResponse>> Get(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await FindAsync(clientId, id, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<NoteResponse>> Create(int clientId, SaveNoteRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var item = new Note
        {
            Type = ReferenceEntityType.Client,
            ReferenceId = clientId,
            Notes = request.Notes,
            CreatedById = _currentUser.UserId,
        };
        _db.Notes.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { clientId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int clientId, int id, SaveNoteRequest request, CancellationToken cancellationToken)
    {
        var item = await FindAsync(clientId, id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Notes = request.Notes;
        item.ModifiedById = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await FindAsync(clientId, id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.Notes.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<Note?> FindAsync(int clientId, int id, CancellationToken cancellationToken) =>
        _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.Type == ReferenceEntityType.Client && n.ReferenceId == clientId, cancellationToken);

    private static NoteResponse ToResponse(Note n) =>
        new(n.Id, n.ReferenceId, n.Type, n.CreatedById, n.ModifiedById, n.Notes, n.CreatedAt);
}
