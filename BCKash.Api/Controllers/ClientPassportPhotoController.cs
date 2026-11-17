using BCKash.Api.Authorization;
using BCKash.Application.Files;
using BCKash.Infrastructure.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// A passport photo uploaded for the client's record while face capture is switched off (Settings →
/// Loan → "Face capture mandatory"): it's their profile picture and nothing more — no liveness check,
/// and never what a loan face match is compared against. A client with an enrolled face already has
/// their profile picture, so it can't be replaced here. Who may upload follows the client's
/// documentation rules (<see cref="ClientRecordAccessAttribute"/>), which also audit a super admin's upload.
/// </summary>
[ApiController]
[Route("api/v1/clients/{clientId:int}/passport-photo")]
[Authorize(Policy = "Permission:clients.manage")]
[ClientRecordAccess]
public class ClientPassportPhotoController : ControllerBase
{
    private const long MaxPhotoBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private readonly BCKashDbContext _db;
    private readonly IFileStorageService _files;

    public ClientPassportPhotoController(BCKashDbContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    /// <summary>Uploads (or replaces) the client's passport photo — a JPG or PNG of at most 2 MB.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxPhotoBytes + 256 * 1024)]
    public async Task<IActionResult> Upload(int clientId, IFormFile file, CancellationToken cancellationToken)
    {
        var client = await _db.Clients.FirstAsync(c => c.Id == clientId, cancellationToken);

        if (await FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken))
        {
            return Problem(title: "Face capture is mandatory, so the client's photo comes from capturing their face.", statusCode: StatusCodes.Status409Conflict);
        }

        if (client.BiometricEnrolledAt is not null)
        {
            return Problem(title: "The client's face is enrolled — that capture is their photo.", statusCode: StatusCodes.Status409Conflict);
        }

        if (file is null || file.Length == 0)
        {
            return Problem(title: "Choose a photo to upload.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!PhotoExtensions.Contains(Path.GetExtension(file.FileName)))
        {
            return Problem(title: "Upload the passport photo as a JPG or PNG.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxPhotoBytes)
        {
            return Problem(title: "The photo must be 2 MB or smaller.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var name = (client.DisplayName ?? $"{client.FirstName} {client.LastName}").Trim();
        var stored = await _files.SaveAsync(stream, $"{name} - passport photo{Path.GetExtension(file.FileName).ToLowerInvariant()}", cancellationToken);
        client.Picture = stored.Location;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { client.Id, HasPhoto = true });
    }
}
