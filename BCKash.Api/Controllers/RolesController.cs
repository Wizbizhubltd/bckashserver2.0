using System.Text.Json;
using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Which office-portal modules each staff role can open, and which permissions it has — role → permission pairs that can be added
/// and removed. Super admins only. The super admin role is locked to every permission, so access
/// management can never be removed from everyone. Changes reach a staff member's session when their
/// access token is next renewed (within its lifetime, ~15 minutes) or at their next sign-in.
/// </summary>
[ApiController]
[Route("api/v1/roles")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public class RolesController : ControllerBase
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public RolesController(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Every permission that can be granted, grouped by area in catalogue order.</summary>
    [HttpGet("permissions")]
    public ActionResult<IReadOnlyList<PermissionResponse>> Permissions() =>
        Ok(PermissionCatalog.All.Select(p => new PermissionResponse(p.Slug, p.Area, p.Name, p.Description)).ToList());

    /// <summary>Every office-portal module that can be ticked for a role, in the order the portal shows them.</summary>
    [HttpGet("modules")]
    public ActionResult<IReadOnlyList<ModuleResponse>> Modules() =>
        Ok(OfficePortalModules.All.Select(m => new ModuleResponse(m.Slug, m.Name, m.Description)).ToList());

    /// <summary>Lets everyone with the role open an office-portal module. Idempotent.</summary>
    [HttpPost("{id:int}/modules/{slug}")]
    public Task<ActionResult<RoleResponse>> AddModule(int id, string slug, CancellationToken cancellationToken) =>
        ChangeModuleAsync(id, slug, tick: true, cancellationToken);

    /// <summary>Takes an office-portal module away from the role. Idempotent.</summary>
    [HttpDelete("{id:int}/modules/{slug}")]
    public Task<ActionResult<RoleResponse>> RemoveModule(int id, string slug, CancellationToken cancellationToken) =>
        ChangeModuleAsync(id, slug, tick: false, cancellationToken);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> List(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles.Where(r => UserTypeSlugs.All.Contains(r.Slug)).ToListAsync(cancellationToken);
        var responses = new List<RoleResponse>();
        // Most senior first, matching UserTypeSlugs.
        foreach (var role in roles.OrderBy(r => Array.IndexOf(UserTypeSlugs.All.ToArray(), r.Slug)))
        {
            responses.Add(await ToResponseAsync(role, cancellationToken));
        }

        return Ok(responses);
    }

    /// <summary>Grants one permission to a role. Idempotent.</summary>
    [HttpPost("{id:int}/permissions/{slug}")]
    public Task<ActionResult<RoleResponse>> Add(int id, string slug, CancellationToken cancellationToken) =>
        ChangeAsync(id, current => current.Append(slug), cancellationToken);

    /// <summary>Removes one permission from a role. Idempotent.</summary>
    [HttpDelete("{id:int}/permissions/{slug}")]
    public Task<ActionResult<RoleResponse>> Remove(int id, string slug, CancellationToken cancellationToken) =>
        ChangeAsync(id, current => current.Where(s => s != slug), cancellationToken, validate: slug);

    /// <summary>Replaces a role's permissions with exactly <see cref="SetRolePermissionsRequest.Permissions"/>.</summary>
    [HttpPut("{id:int}/permissions")]
    public Task<ActionResult<RoleResponse>> Set(int id, SetRolePermissionsRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, _ => request.Permissions, cancellationToken);

    private async Task<ActionResult<RoleResponse>> ChangeAsync(
        int id, Func<IEnumerable<string>, IEnumerable<string>> change, CancellationToken cancellationToken, string? validate = null)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && UserTypeSlugs.All.Contains(r.Slug), cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        if (role.Slug == UserTypeSlugs.SuperAdmin)
        {
            return Problem(title: "The Super Admin role always has every permission and can't be changed.", statusCode: StatusCodes.Status409Conflict);
        }

        var granted = await _db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Select(rp => new { rp.PermissionId, rp.Permission.Slug })
            .ToListAsync(cancellationToken);
        // Only catalogue permissions are managed here; any legacy grant on the role is left alone.
        var current = granted.Where(g => g.Slug != null && PermissionCatalog.Slugs.Contains(g.Slug)).Select(g => g.Slug!).ToHashSet();
        var wanted = change(current).Select(s => s.Trim()).ToHashSet();

        var unknown = wanted.Concat(validate is null ? [] : [validate]).Where(s => !PermissionCatalog.Slugs.Contains(s)).ToList();
        if (unknown.Count > 0)
        {
            return Problem(title: $"Unknown permission: {string.Join(", ", unknown)}.", statusCode: StatusCodes.Status400BadRequest);
        }

        var added = wanted.Except(current).ToList();
        var removed = current.Except(wanted).ToList();
        if (added.Count > 0 || removed.Count > 0)
        {
            var permissionIds = await _db.Permissions
                .Where(p => p.Slug != null && (added.Contains(p.Slug) || removed.Contains(p.Slug)))
                .ToDictionaryAsync(p => p.Slug!, p => p.Id, cancellationToken);

            var removedIds = removed.Select(s => permissionIds[s]).ToHashSet();
            _db.RolePermissions.RemoveRange(await _db.RolePermissions.Where(rp => rp.RoleId == id && removedIds.Contains(rp.PermissionId)).ToListAsync(cancellationToken));
            foreach (var slug in added)
            {
                _db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permissionIds[slug] });
            }

            // RolePermission is a plain join row the audit interceptor doesn't see — record the change explicitly.
            _db.AuditTrail.Add(new AuditTrailEntry
            {
                UserId = _currentUser.UserId,
                Module = "Role",
                Action = "Update",
                Notes = JsonSerializer.Serialize(new { role = role.Slug, added, removed }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            role.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(await ToResponseAsync(role, cancellationToken));
    }

    private async Task<ActionResult<RoleResponse>> ChangeModuleAsync(int id, string slug, bool tick, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && UserTypeSlugs.All.Contains(r.Slug), cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        if (role.Slug == UserTypeSlugs.SuperAdmin)
        {
            return Problem(title: "Super admins use the control portal, so they have no office-portal modules.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!OfficePortalModules.Slugs.Contains(slug))
        {
            return Problem(title: $"Unknown module: {slug}.", statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await _db.RoleModules.FirstOrDefaultAsync(rm => rm.RoleId == id && rm.Module == slug, cancellationToken);
        if (tick == (existing is not null))
        {
            return Ok(await ToResponseAsync(role, cancellationToken));
        }

        if (existing is null)
        {
            _db.RoleModules.Add(new RoleModule { RoleId = id, Module = slug });
        }
        else
        {
            _db.RoleModules.Remove(existing);
        }

        // RoleModule is a plain join row the audit interceptor doesn't see — record the change explicitly.
        _db.AuditTrail.Add(new AuditTrailEntry
        {
            UserId = _currentUser.UserId,
            Module = "Role",
            Action = "Update",
            Notes = JsonSerializer.Serialize(new { role = role.Slug, modulesAdded = tick ? new[] { slug } : [], modulesRemoved = tick ? [] : new[] { slug } }),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await ToResponseAsync(role, cancellationToken));
    }

    private async Task<RoleResponse> ToResponseAsync(Role role, CancellationToken cancellationToken)
    {
        var slugs = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id && rp.Permission.Slug != null)
            .Select(rp => rp.Permission.Slug!)
            .ToListAsync(cancellationToken);
        var staffCount = await _db.RoleUsers.CountAsync(ru => ru.RoleId == role.Id, cancellationToken);
        var locked = role.Slug == UserTypeSlugs.SuperAdmin;
        var permissions = locked
            ? PermissionCatalog.All.Select(p => p.Slug).ToList()
            : PermissionCatalog.All.Select(p => p.Slug).Where(slugs.Contains).ToList();
        var ticked = await _db.RoleModules.Where(rm => rm.RoleId == role.Id).Select(rm => rm.Module).ToListAsync(cancellationToken);
        var modules = OfficePortalModules.All.Select(m => m.Slug).Where(ticked.Contains).ToList();
        return new RoleResponse(role.Id, role.Slug, role.Name, staffCount, locked, permissions, modules);
    }
}
