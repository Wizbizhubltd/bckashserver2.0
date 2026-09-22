using BCKash.Api.Contracts;
using BCKash.Application.Files;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Client document upload/download (FR-CLI-4), wired to the abstracted <see cref="IFileStorageService"/>.
/// Scoped to clients in Phase 2, but Document itself is polymorphic (BCKash.Domain.Clients.Document)
/// and reusable by loans/groups/savings/etc. in later phases via the same Type+RecordId shape.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:int}/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public DocumentsController(BCKashDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DocumentResponse>>> List(int clientId, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.Documents
            .Where(d => d.Type == ReferenceEntityType.Client && d.RecordId == clientId)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<DocumentResponse>> Upload(int clientId, IFormFile file, [FromForm] string? notes, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        if (file.Length == 0)
        {
            return Problem(title: "A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var stored = await _fileStorage.SaveAsync(stream, file.FileName, cancellationToken);

        var document = new Document
        {
            Type = ReferenceEntityType.Client,
            RecordId = clientId,
            Name = file.FileName,
            Size = stored.SizeBytes.ToString(),
            Location = stored.Location,
            Notes = notes,
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), new { clientId }, ToResponse(document));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int clientId, int id, CancellationToken cancellationToken)
    {
        var document = await FindAsync(clientId, id, cancellationToken);
        if (document?.Location is null)
        {
            return NotFound();
        }

        var content = await _fileStorage.ReadAsync(document.Location, document.Name ?? string.Empty, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        return File(content.Content, content.ContentType, document.Name);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken cancellationToken)
    {
        var document = await FindAsync(clientId, id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);

        if (document.Location is not null)
        {
            await _fileStorage.DeleteAsync(document.Location, cancellationToken);
        }

        return NoContent();
    }

    private Task<Document?> FindAsync(int clientId, int id, CancellationToken cancellationToken) =>
        _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.Type == ReferenceEntityType.Client && d.RecordId == clientId, cancellationToken);

    private static DocumentResponse ToResponse(Document d) =>
        new(d.Id, d.Type, d.RecordId, d.Name, d.Size, d.Notes, d.CreatedAt);
}
