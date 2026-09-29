using BCKash.Api.Contracts;
using BCKash.Domain.Clients;
using BCKash.Domain.Loans;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// CRUD over the `settings` table. Most keys are still stored-only; keys the system acts on are
/// validated here before they're saved — the company profile (CompanyProfileRules) and the overdue
/// &amp; penalty rules (OverdueRuleKeys).
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private const string ManageSettingsPolicy = "Permission:settings.manage";

    private readonly BCKashDbContext _db;

    public SettingsController(BCKashDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Every stored setting. Needs settings.manage: the table holds secrets (e.g. the reCAPTCHA
    /// secret key), so ordinary staff read only what they need via <see cref="Display"/>.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<ActionResult<IReadOnlyCollection<SettingResponse>>> List(CancellationToken cancellationToken)
    {
        var settings = await _db.Settings
            .OrderByDescending(s => s.Id)
            .Select(s => new SettingResponse(s.Id, s.SettingKey, s.SettingValue))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    /// <summary>What every signed-in portal needs to show money correctly — safe for any staff member.</summary>
    [HttpGet("display")]
    public async Task<ActionResult<DisplaySettingsResponse>> Display([FromServices] ICurrencyDisplayProvider currency, CancellationToken cancellationToken)
    {
        var display = await currency.GetAsync(cancellationToken);
        return Ok(new DisplaySettingsResponse(display.Symbol, display.Position == CurrencySymbolPosition.Right ? "right" : "left"));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<ActionResult<SettingResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings.FindAsync([id], cancellationToken);
        return setting is null ? NotFound() : Ok(new SettingResponse(setting.Id, setting.SettingKey, setting.SettingValue));
    }

    [HttpPost]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<ActionResult<SettingResponse>> Create(CreateSettingRequest request, CancellationToken cancellationToken)
    {
        var value = Normalize(request.SettingKey, request.SettingValue);
        var invalid = Validate(request.SettingKey, value);
        if (invalid is not null)
        {
            return Problem(title: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        var setting = new Setting { SettingKey = request.SettingKey, SettingValue = value };
        _db.Settings.Add(setting);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = setting.Id }, new SettingResponse(setting.Id, setting.SettingKey, setting.SettingValue));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<IActionResult> Update(int id, UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings.FindAsync([id], cancellationToken);
        if (setting is null)
        {
            return NotFound();
        }

        var value = Normalize(setting.SettingKey, request.SettingValue);
        var invalid = Validate(setting.SettingKey, value);
        if (invalid is not null)
        {
            return Problem(title: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        setting.SettingValue = value;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings.FindAsync([id], cancellationToken);
        if (setting is null)
        {
            return NotFound();
        }

        if (setting.SettingKey == CompanyProfileKeys.Name)
        {
            return Problem(title: "The company name can be changed but not removed.", statusCode: StatusCodes.Status400BadRequest);
        }

        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Values of keys the system acts on are stored trimmed; every other key is stored exactly as sent.</summary>
    private static string? Normalize(string key, string? value) =>
        CompanyProfileKeys.All.Contains(key) || OverdueRuleKeys.All.Contains(key) || CurrencyDisplayKeys.All.Contains(key) || key == IOfficeFundService.RequireFundsSettingKey
        || key == LoanFaceMatchRoles.SettingKey || key == ISmsSwitch.SettingKey || ClientSavingsSettingKeys.All.Contains(key) || key == FaceCaptureRules.SettingKey
            ? value?.Trim()
            : value;

    /// <summary>Rules for the keys the system acts on: company profile, overdue &amp; penalty rules, currency display, office funds, loan face match roles, SMS sending.</summary>
    private static string? Validate(string key, string? value) =>
        CompanyProfileRules.Validate(key, value) ?? OverdueRuleKeys.Validate(key, value) ?? CurrencyDisplayKeys.Validate(key, value) ?? LoanFaceMatchRoles.Validate(key, value)
        ?? ClientSavingsSettingKeys.Validate(key, value)
        ?? FaceCaptureRules.Validate(key, value)
        ?? (key == IOfficeFundService.RequireFundsSettingKey && value?.Trim() is not ("0" or "1") ? "“Loans draw on office funds” must be switched on (1) or off (0)." : null)
        ?? (key == ISmsSwitch.SettingKey && value?.Trim() is not ("0" or "1") ? "“SMS sending” must be switched on (1) or off (0)." : null);
}
