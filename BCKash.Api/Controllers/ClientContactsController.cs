using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Application.Files;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// A client's guarantors (<c>/guarantors</c>) and references (<c>/references</c>). They're part of the
/// client's documentation, so only the staff member who onboarded the client changes them. A client
/// needs at least 2 guarantors and 1 reference to be approved, and once approved can't drop below that.
/// A guarantor can carry a passport photo (<c>/{id}/photo</c>), printed on the client's loan form.
/// </summary>
[ApiController]
[Route("api/v1/clients/{clientId:int}/{kind:regex(^(guarantors|references)$)}")]
[ClientRecordAccess]
[Authorize]
public class ClientContactsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";
    private const long MaxPhotoBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };
    private static readonly HashSet<string> Genders = ["male", "female", "other"];

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IFileStorageService _files;

    public ClientContactsController(BCKashDbContext db, ICurrentUserContext currentUser, IFileStorageService files)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientContactResponse>>> List(int clientId, string kind, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        var contacts = await _db.ClientContacts
            .Where(c => c.ClientId == clientId && c.Kind == contactKind)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
        return Ok(contacts.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Create(int clientId, string kind, SaveClientContactRequest request, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        if (Validate(contactKind, request) is { } problem)
        {
            return Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
        }

        var contact = new ClientContact
        {
            ClientId = clientId,
            Kind = contactKind,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        Apply(contact, request);
        _db.ClientContacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ToResponse(contact));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int clientId, string kind, int id, SaveClientContactRequest request, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        var contact = await _db.ClientContacts.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId && c.Kind == contactKind, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        if (Validate(contactKind, request) is { } problem)
        {
            return Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
        }

        Apply(contact, request);
        contact.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(contact));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int clientId, string kind, int id, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        var contact = await _db.ClientContacts.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId && c.Kind == contactKind, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        // An approved client must keep the minimum — add a replacement before removing one.
        var client = await _db.Clients.FirstAsync(c => c.Id == clientId, cancellationToken);
        var minimum = contactKind == ClientContact.GuarantorKind ? ClientContact.MinimumGuarantors : ClientContact.MinimumReferences;
        var count = await _db.ClientContacts.CountAsync(c => c.ClientId == clientId && c.Kind == contactKind, cancellationToken);
        if (ClientDeletionRules.WasApproved(client) && count <= minimum)
        {
            return Problem(
                title: $"An approved client needs at least {minimum} {(contactKind == ClientContact.GuarantorKind ? "guarantors" : "reference")}. Add another before removing this one.",
                statusCode: StatusCodes.Status409Conflict);
        }

        _db.ClientContacts.Remove(contact);
        await _db.SaveChangesAsync(cancellationToken);
        if (contact.Photo is not null)
        {
            await _files.DeleteAsync(contact.Photo, cancellationToken);
        }

        return NoContent();
    }

    /// <summary>Uploads (or replaces) the contact's passport photo — a JPG or PNG of at most 2 MB.</summary>
    [HttpPost("{id:int}/photo")]
    [Authorize(Policy = ManagePolicy)]
    [RequestSizeLimit(MaxPhotoBytes + 256 * 1024)]
    public async Task<IActionResult> UploadPhoto(int clientId, string kind, int id, IFormFile file, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        var contact = await _db.ClientContacts.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId && c.Kind == contactKind, cancellationToken);
        if (contact is null)
        {
            return NotFound();
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
        var stored = await _files.SaveAsync(stream, $"{contact.FullName} - passport{Path.GetExtension(file.FileName).ToLowerInvariant()}", cancellationToken);
        var previous = contact.Photo;
        contact.Photo = stored.Location;
        contact.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            await _files.DeleteAsync(previous, cancellationToken);
        }

        return Ok(ToResponse(contact));
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> GetPhoto(int clientId, string kind, int id, CancellationToken cancellationToken)
    {
        var contactKind = KindFor(kind);
        var location = await _db.ClientContacts
            .Where(c => c.Id == id && c.ClientId == clientId && c.Kind == contactKind)
            .Select(c => c.Photo)
            .FirstOrDefaultAsync(cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        var content = await _files.ReadAsync(location, location, cancellationToken);
        return content is null ? NotFound() : File(content.Content, content.ContentType);
    }

    private static string KindFor(string routeKind) => routeKind == "guarantors" ? ClientContact.GuarantorKind : ClientContact.ReferenceKind;

    private static string? Validate(string kind, SaveClientContactRequest request)
    {
        if ((request.FullName?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) < 2)
        {
            return "Enter the full name — at least a first and last name.";
        }

        var phoneDigits = new string((request.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (phoneDigits.Length != 11)
        {
            return "Enter an 11-digit phone number, e.g. 08031234567.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            return "Enter a valid email address.";
        }

        if (!string.IsNullOrWhiteSpace(request.Gender) && !Genders.Contains(request.Gender.Trim().ToLowerInvariant()))
        {
            return "Gender must be male, female or other.";
        }

        if (string.IsNullOrWhiteSpace(request.Relationship))
        {
            return "Say how they know the client.";
        }

        return kind == ClientContact.GuarantorKind && string.IsNullOrWhiteSpace(request.Address)
            ? "A guarantor's address is required."
            : null;
    }

    private static void Apply(ClientContact contact, SaveClientContactRequest request)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        contact.FullName = request.FullName.Trim();
        contact.Phone = Clean(request.Phone);
        contact.Email = Clean(request.Email);
        contact.Address = Clean(request.Address);
        contact.Relationship = Clean(request.Relationship);
        contact.Occupation = Clean(request.Occupation);
        contact.Gender = Clean(request.Gender)?.ToLowerInvariant();
    }

    private static ClientContactResponse ToResponse(ClientContact c) =>
        new(c.Id, c.Kind, c.FullName, c.Phone, c.Email, c.Address, c.Relationship, c.Occupation, c.CreatedAt, c.Gender, c.Photo is not null);
}
