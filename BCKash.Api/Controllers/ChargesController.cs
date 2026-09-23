using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/charges")]
[Authorize]
public class ChargesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;

    public ChargesController(BCKashDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ChargeResponse>>> List([FromQuery] ChargeProduct? product, CancellationToken cancellationToken)
    {
        var query = _db.Charges.AsQueryable();
        if (product.HasValue)
        {
            query = query.Where(c => c.Product == product.Value);
        }

        var charges = await query.ToListAsync(cancellationToken);
        return Ok(charges.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ChargeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        return charge is null ? NotFound() : Ok(ToResponse(charge));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ChargeResponse>> Create(SaveChargeRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var charge = new Charge
        {
            Name = request.Name,
            CurrencyId = request.CurrencyId,
            Product = request.Product,
            ChargeType = request.ChargeType,
            ChargeOption = request.ChargeOption,
            ChargeFrequency = request.ChargeFrequency,
            ChargeFrequencyType = request.ChargeFrequencyType,
            ChargeFrequencyAmount = request.ChargeFrequencyAmount,
            Amount = request.Amount,
            MinimumAmount = request.MinimumAmount,
            MaximumAmount = request.MaximumAmount,
            ChargePaymentMode = request.ChargePaymentMode,
            Penalty = request.Penalty,
            Override = request.Override,
            GlAccountIncomeId = request.GlAccountIncomeId,
        };
        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = charge.Id }, ToResponse(charge));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveChargeRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        if (charge is null)
        {
            return NotFound();
        }

        charge.Name = request.Name;
        charge.CurrencyId = request.CurrencyId;
        charge.Product = request.Product;
        charge.ChargeType = request.ChargeType;
        charge.ChargeOption = request.ChargeOption;
        charge.ChargeFrequency = request.ChargeFrequency;
        charge.ChargeFrequencyType = request.ChargeFrequencyType;
        charge.ChargeFrequencyAmount = request.ChargeFrequencyAmount;
        charge.Amount = request.Amount;
        charge.MinimumAmount = request.MinimumAmount;
        charge.MaximumAmount = request.MaximumAmount;
        charge.ChargePaymentMode = request.ChargePaymentMode;
        charge.Penalty = request.Penalty;
        charge.Override = request.Override;
        charge.GlAccountIncomeId = request.GlAccountIncomeId;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(charge));
    }

    /// <summary>
    /// Deactivates rather than deletes when the charge is already attached to a product
    /// (FR-ORG-5: "cannot be deleted if in use on an active account — deactivate instead").
    /// Phase 1 has no loan/savings-product-charge data yet to check against, so this always
    /// deactivates; later phases should add the in-use check here once those tables are populated.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        if (charge is null)
        {
            return NotFound();
        }

        charge.Active = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(SaveChargeRequest request)
    {
        if (!ChargeValidationRules.IsValidChargeTypeForProduct(request.ChargeType, request.Product))
        {
            return $"Charge type '{request.ChargeType}' is not valid for product '{request.Product}'.";
        }

        if (!ChargeValidationRules.IsValidChargeOptionForProduct(request.ChargeOption, request.Product))
        {
            return $"Charge option '{request.ChargeOption}' is only valid for loan charges.";
        }

        return null;
    }

    private static ChargeResponse ToResponse(Charge c) => new(
        c.Id, c.Name, c.CurrencyId, c.Product, c.ChargeType, c.ChargeOption, c.ChargeFrequency,
        c.ChargeFrequencyType, c.ChargeFrequencyAmount, c.Amount, c.MinimumAmount, c.MaximumAmount,
        c.ChargePaymentMode, c.Active, c.Penalty, c.Override, c.GlAccountIncomeId);
}
