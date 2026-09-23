using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/custom-fields")]
[Authorize]
public class CustomFieldsController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public CustomFieldsController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CustomFieldResponse>>> List([FromQuery] string? category, CancellationToken cancellationToken)
    {
        var query = _db.CustomFields.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(f => f.Category == category);
        }

        var fields = await query.ToListAsync(cancellationToken);
        return Ok(fields.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomFieldResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var field = await _db.CustomFields.FindAsync([id], cancellationToken);
        return field is null ? NotFound() : Ok(ToResponse(field));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<CustomFieldResponse>> Create(SaveCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var field = new CustomField
        {
            Category = request.Category,
            Name = request.Name,
            FieldType = request.FieldType,
            Required = request.Required,
            RadioBoxValues = request.RadioBoxValues,
            CheckboxValues = request.CheckboxValues,
            SelectValues = request.SelectValues,
        };
        _db.CustomFields.Add(field);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = field.Id }, ToResponse(field));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var field = await _db.CustomFields.FindAsync([id], cancellationToken);
        if (field is null)
        {
            return NotFound();
        }

        field.Category = request.Category;
        field.Name = request.Name;
        field.FieldType = request.FieldType;
        field.Required = request.Required;
        field.RadioBoxValues = request.RadioBoxValues;
        field.CheckboxValues = request.CheckboxValues;
        field.SelectValues = request.SelectValues;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(field));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var field = await _db.CustomFields.FindAsync([id], cancellationToken);
        if (field is null)
        {
            return NotFound();
        }

        _db.CustomFields.Remove(field);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---- Values (BR-ORG-6 — capturing a value against a record; see CustomFieldValue) ----

    [HttpGet("values")]
    public async Task<ActionResult<IReadOnlyCollection<CustomFieldValueResponse>>> ListValues(
        [FromQuery] string entityType, [FromQuery] int entityId, CancellationToken cancellationToken)
    {
        var values = await _db.CustomFieldValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .ToListAsync(cancellationToken);

        return Ok(values.Select(ToValueResponse).ToList());
    }

    [HttpPost("values")]
    public async Task<ActionResult<CustomFieldValueResponse>> CaptureValue(CaptureCustomFieldValueRequest request, CancellationToken cancellationToken)
    {
        var field = await _db.CustomFields.FindAsync([request.CustomFieldId], cancellationToken);
        if (field is null)
        {
            return Problem(title: "Unknown custom field.", statusCode: StatusCodes.Status400BadRequest);
        }

        var validationError = CustomFieldValueValidator.Validate(field, request.Value);
        if (validationError is not null)
        {
            return Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await _db.CustomFieldValues.FirstOrDefaultAsync(
            v => v.CustomFieldId == request.CustomFieldId && v.EntityType == request.EntityType && v.EntityId == request.EntityId,
            cancellationToken);

        if (existing is not null)
        {
            existing.Value = request.Value;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToValueResponse(existing));
        }

        var value = new CustomFieldValue
        {
            CustomFieldId = request.CustomFieldId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Value = request.Value,
        };
        _db.CustomFieldValues.Add(value);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ListValues), new { entityType = value.EntityType, entityId = value.EntityId }, ToValueResponse(value));
    }

    private static CustomFieldResponse ToResponse(CustomField f) =>
        new(f.Id, f.Category, f.Name, f.FieldType, f.Required, f.RadioBoxValues, f.CheckboxValues, f.SelectValues);

    private static CustomFieldValueResponse ToValueResponse(CustomFieldValue v) =>
        new(v.Id, v.CustomFieldId, v.EntityType, v.EntityId, v.Value);
}
