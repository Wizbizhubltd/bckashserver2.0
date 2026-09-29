using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Application.Files;
using BCKash.Application.Identity;
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
[Route("api/v1/clients/{clientId:int}/documents")]
[ClientRecordAccess]
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
            .OrderByDescending(d => d.Id)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Uploads one of the client's documents (see ClientDocumentRules): <c>category</c> nin_slip,
    /// utility_bill or id_card, with <c>idNumber</c> (the NIN, the bill's account/meter number, or the
    /// ID's number) and, for an id_card, <c>idType</c>. A JPG, PNG or PDF of at most 2 MB. It replaces
    /// the client's previous document of the same kind.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    [RequestSizeLimit(ClientDocumentRules.MaxFileBytes + 256 * 1024)]
    public async Task<ActionResult<DocumentResponse>> Upload(
        int clientId,
        IFormFile file,
        [FromForm] string? category,
        [FromForm] string? idType,
        [FromForm] string? idNumber,
        [FromForm] string? notes,
        [FromServices] IOfficeScope scope,
        CancellationToken cancellationToken)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return Problem(title: "A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();
        idType = string.IsNullOrWhiteSpace(idType) ? null : idType.Trim().ToLowerInvariant();

        // Office staff upload only the three client documents; the free-form upload stays for legacy accounts with no user_type.
        if (category is null && await scope.GetUserTypeAsync(cancellationToken) is not null)
        {
            return Problem(title: "Choose which document this is: NIN slip, utility bill or ID card.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (category is not null)
        {
            if (!ClientDocumentRules.Categories.Contains(category))
            {
                return Problem(title: "Unknown document type.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (ClientDocumentRules.NumberProblem(category, idType, idNumber) is { } problem)
            {
                return Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
            }

            if (!ClientDocumentRules.AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
            {
                return Problem(title: "Upload a picture (JPG or PNG) or a PDF.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (file.Length > ClientDocumentRules.MaxFileBytes)
            {
                return Problem(title: "The file must be 2 MB or smaller.", statusCode: StatusCodes.Status400BadRequest);
            }

            // One NIN belongs to one person.
            var nin = idNumber!.Trim();
            if (category == ClientDocumentRules.NinSlip && await _db.Documents.AnyAsync(
                    d => d.Category == ClientDocumentRules.NinSlip && d.IdNumber == nin && d.RecordId != clientId && d.Type == ReferenceEntityType.Client, cancellationToken))
            {
                return Problem(title: "This NIN is already on another client.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        // Stored under the client's full name so files are recognisable in the bucket, e.g. "Ada Obi - NIN slip.pdf".
        var clientName = !string.IsNullOrWhiteSpace(client.DisplayName)
            ? client.DisplayName
            : string.Join(' ', new[] { client.FirstName, client.MiddleName, client.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var storedName = category is null
            ? $"{clientName} - {file.FileName}"
            : $"{clientName} - {ClientDocumentRules.Label(category, idType)}{Path.GetExtension(file.FileName).ToLowerInvariant()}";

        await using var stream = file.OpenReadStream();
        var stored = await _fileStorage.SaveAsync(stream, storedName, cancellationToken);

        var replaced = category is null
            ? []
            : await _db.Documents.Where(d => d.Type == ReferenceEntityType.Client && d.RecordId == clientId && d.Category == category).ToListAsync(cancellationToken);

        var document = new Document
        {
            Type = ReferenceEntityType.Client,
            RecordId = clientId,
            Name = file.FileName,
            Size = stored.SizeBytes.ToString(),
            Location = stored.Location,
            Notes = notes,
            Category = category,
            IdType = category == ClientDocumentRules.IdCard ? idType : null,
            IdNumber = category is null ? null : idNumber!.Trim().ToUpperInvariant(),
        };
        _db.Documents.Add(document);
        _db.Documents.RemoveRange(replaced);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var old in replaced.Where(d => d.Location is not null))
        {
            await _fileStorage.DeleteAsync(old.Location!, cancellationToken);
        }

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
        new(d.Id, d.Type, d.RecordId, d.Name, d.Size, d.Notes, d.CreatedAt,
            d.Category, d.IdType, d.IdNumber, d.Category is null ? null : ClientDocumentRules.Label(d.Category, d.IdType));
}
