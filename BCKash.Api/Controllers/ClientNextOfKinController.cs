using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:int}/next-of-kin")]
[ClientRecordAccess]
[Authorize]
public class ClientNextOfKinController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";

    private readonly BCKashDbContext _db;

    public ClientNextOfKinController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientNextOfKinResponse>>> List(int clientId, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _db.ClientNextOfKin.Where(k => k.ClientId == clientId).OrderByDescending(k => k.Id).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientNextOfKinResponse>> Get(int clientId, int id, CancellationToken cancellationToken)
    {
        var item = await _db.ClientNextOfKin.FirstOrDefaultAsync(k => k.Id == id && k.ClientId == clientId, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientNextOfKinResponse>> Create(int clientId, SaveClientNextOfKinRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return NotFound();
        }

        if (await ValidateAsync(request, cancellationToken) is { } problem)
        {
            return Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
        }

        var item = new ClientNextOfKin
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
        _db.ClientNextOfKin.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { clientId, id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int clientId, int id, SaveClientNextOfKinRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.ClientNextOfKin.FirstOrDefaultAsync(k => k.Id == id && k.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (await ValidateAsync(request, cancellationToken) is { } problem)
        {
            return Problem(title: problem, statusCode: StatusCodes.Status400BadRequest);
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
        var item = await _db.ClientNextOfKin.FirstOrDefaultAsync(k => k.Id == id && k.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.ClientNextOfKin.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// What a next of kin needs before it's saved. Without these checks a relationship id that isn't one
    /// of the configured relationship types failed the save with a database error (HTTP 500).
    /// </summary>
    private async Task<string?> ValidateAsync(SaveClientNextOfKinRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientRelationshipId is not { } relationshipId)
        {
            return "Choose how they're related to the client.";
        }

        if (!await _db.ClientRelationships.AnyAsync(r => r.Id == relationshipId, cancellationToken))
        {
            return "That relationship type doesn't exist — choose one from the list.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return "Enter the next of kin's first and last name.";
        }

        var mobileDigits = new string((request.Mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!string.IsNullOrWhiteSpace(request.Mobile) && mobileDigits.Length != 11 && !(mobileDigits.Length == 13 && mobileDigits.StartsWith("234", StringComparison.Ordinal)))
        {
            return "Enter an 11-digit phone number, e.g. 08031234567.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            return "Enter a valid email address.";
        }

        return null;
    }

    private static ClientNextOfKinResponse ToResponse(ClientNextOfKin k) => new(
        k.Id, k.ClientId, k.ClientRelationshipId, k.Qualification, k.FirstName, k.MiddleName, k.LastName,
        k.Ward, k.Street, k.District, k.Region, k.Address, k.Mobile, k.Phone, k.Email, k.Gender, k.Notes);
}
