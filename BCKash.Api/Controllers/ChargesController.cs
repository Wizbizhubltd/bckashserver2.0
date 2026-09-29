using BCKash.Api.Contracts;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/charges")]
[Authorize]
public class ChargesController : ControllerBase
{
    private const string ManagePolicy = "Permission:organization.manage";

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ChargesController(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ChargeResponse>>> List([FromQuery] ChargeProduct? product, CancellationToken cancellationToken)
    {
        var query = _db.Charges.AsQueryable();
        if (product.HasValue)
        {
            query = query.Where(c => c.Product == product.Value);
        }

        var charges = await query.OrderByDescending(c => c.Id).ToListAsync(cancellationToken);
        var usage = await UsageCountsAsync(charges.Select(c => c.Id).ToList(), cancellationToken);
        return Ok(charges.Select(c => ToResponse(c, usage.GetValueOrDefault(c.Id))).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ChargeResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        return charge is null ? NotFound() : Ok(await ToResponseAsync(charge, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ChargeResponse>> Create(SaveChargeRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(request, null, cancellationToken);
        if (validationError is not null)
        {
            return Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var charge = new Charge { CreatedById = _currentUser.UserId, CreatedAt = DateTime.UtcNow };
        Apply(charge, request);
        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = charge.Id }, await ToResponseAsync(charge, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, SaveChargeRequest request, CancellationToken cancellationToken)
    {
        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        if (charge is null)
        {
            return NotFound();
        }

        var validationError = await ValidateAsync(request, id, cancellationToken);
        if (validationError is not null)
        {
            return Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        Apply(charge, request);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await ToResponseAsync(charge, cancellationToken));
    }

    /// <summary>
    /// Deactivates rather than deletes (FR-ORG-5: "cannot be deleted if in use on an active account —
    /// deactivate instead") — charges already attached to loans and accounts keep pointing at it.
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
        charge.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ChargeResponse>> Activate(int id, CancellationToken cancellationToken)
    {
        var charge = await _db.Charges.FindAsync([id], cancellationToken);
        if (charge is null)
        {
            return NotFound();
        }

        if (await NameTakenAsync(charge.Name, charge.Product, id, cancellationToken))
        {
            return Problem(title: "Another active fee already uses this name — rename one of them first.", statusCode: StatusCodes.Status409Conflict);
        }

        charge.Active = true;
        charge.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(charge, cancellationToken));
    }

    private async Task<string?> ValidateAsync(SaveChargeRequest request, int? id, CancellationToken cancellationToken)
    {
        // Legacy product/option scoping first, then the fee and penalty rules.
        if (!ChargeValidationRules.IsValidChargeOptionForProduct(request.ChargeOption, request.Product))
        {
            return $"Charge option '{request.ChargeOption}' is only valid for loan charges.";
        }

        var ruleError = ChargeRules.Validate(new ChargeRules.Definition(
            request.Name, request.Product, request.ChargeType, request.ChargeOption,
            request.Amount, request.MinimumAmount, request.MaximumAmount,
            request.GraceDays, request.RepeatEveryDays, request.MaxTotalPercent, request.FreeAfterInstallments));
        if (ruleError is not null)
        {
            return ruleError;
        }

        if (await NameTakenAsync(request.Name, request.Product, id, cancellationToken))
        {
            return "An active fee with this name already exists at this level.";
        }

        if (request.GlAccountIncomeId.HasValue
            && !await _db.GlAccounts.AnyAsync(g => g.Id == request.GlAccountIncomeId && g.AccountType == GlAccountType.Income, cancellationToken))
        {
            return "The income account must be an existing Income account in the chart of accounts.";
        }

        return null;
    }

    /// <summary>Names are unique among active charges at the same level (loan, client, …), ignoring case and surrounding spaces.</summary>
    private async Task<bool> NameTakenAsync(string? name, ChargeProduct product, int? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name?.Trim().ToLower();
        if (string.IsNullOrEmpty(lowered))
        {
            return false;
        }

        return await _db.Charges.AnyAsync(
            c => c.Id != excludeId && c.Active && c.Product == product && c.Name != null && c.Name.Trim().ToLower() == lowered,
            cancellationToken);
    }

    private static void Apply(Charge charge, SaveChargeRequest request)
    {
        charge.Name = request.Name?.Trim();
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
        charge.Penalty = ChargeRules.IsPenalty(request.ChargeType);
        charge.Override = request.Override;
        charge.GlAccountIncomeId = request.GlAccountIncomeId;
        charge.GraceDays = request.GraceDays;
        charge.RepeatEveryDays = request.RepeatEveryDays;
        charge.MaxTotalPercent = request.MaxTotalPercent;
        charge.FreeAfterInstallments = request.FreeAfterInstallments;
        charge.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<Dictionary<int, int>> UsageCountsAsync(List<int> chargeIds, CancellationToken cancellationToken)
    {
        var references = new List<int?>();
        references.AddRange(await _db.LoanCharges.Where(x => x.ChargeId.HasValue && chargeIds.Contains(x.ChargeId.Value)).Select(x => x.ChargeId).ToListAsync(cancellationToken));
        references.AddRange(await _db.LoanProductCharges.Where(x => x.ChargeId.HasValue && chargeIds.Contains(x.ChargeId.Value)).Select(x => x.ChargeId).ToListAsync(cancellationToken));
        references.AddRange(await _db.SavingsCharges.Where(x => x.ChargeId.HasValue && chargeIds.Contains(x.ChargeId.Value)).Select(x => x.ChargeId).ToListAsync(cancellationToken));
        references.AddRange(await _db.SavingsProductCharges.Where(x => x.ChargeId.HasValue && chargeIds.Contains(x.ChargeId.Value)).Select(x => x.ChargeId).ToListAsync(cancellationToken));
        return references.GroupBy(r => r!.Value).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<ChargeResponse> ToResponseAsync(Charge c, CancellationToken cancellationToken) =>
        ToResponse(c, (await UsageCountsAsync([c.Id], cancellationToken)).GetValueOrDefault(c.Id));

    private static ChargeResponse ToResponse(Charge c, int usageCount) => new(
        c.Id, c.Name, c.CurrencyId, c.Product, c.ChargeType, c.ChargeOption, c.ChargeFrequency,
        c.ChargeFrequencyType, c.ChargeFrequencyAmount, c.Amount, c.MinimumAmount, c.MaximumAmount,
        c.ChargePaymentMode, c.Active, c.Penalty, c.Override, c.GlAccountIncomeId,
        c.GraceDays, c.RepeatEveryDays, c.MaxTotalPercent, c.FreeAfterInstallments, usageCount);
}
