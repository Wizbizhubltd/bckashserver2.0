using BCKash.Api.Contracts;
using BCKash.Domain.Organization;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Thin CRUD over the `settings` table. Its real purpose in Phase 0 is to be the
/// "dummy audited entity" and "dummy permission-gated endpoint" the acceptance
/// criteria call for — proving the audit interceptor and permission policies work
/// end-to-end before any real module exists. Real settings-driven behavior is added
/// by later phases as they need it.
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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SettingResponse>>> List(CancellationToken cancellationToken)
    {
        var settings = await _db.Settings
            .Select(s => new SettingResponse(s.Id, s.SettingKey, s.SettingValue))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SettingResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings.FindAsync([id], cancellationToken);
        return setting is null ? NotFound() : Ok(new SettingResponse(setting.Id, setting.SettingKey, setting.SettingValue));
    }

    [HttpPost]
    [Authorize(Policy = ManageSettingsPolicy)]
    public async Task<ActionResult<SettingResponse>> Create(CreateSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = new Setting { SettingKey = request.SettingKey, SettingValue = request.SettingValue };
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

        setting.SettingValue = request.SettingValue;
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

        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
