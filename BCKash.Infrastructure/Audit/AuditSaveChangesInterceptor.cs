using System.Text.Json;
using BCKash.Domain.Identity;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BCKash.Infrastructure.Audit;

/// <summary>
/// Cross-cutting audit interceptor (FR-SEC-6). Runs inside the same SaveChanges call
/// as the entity change it's auditing, so the audit_trail row and the change it
/// describes commit together or not at all (NFR-2) — no separate write, no risk of
/// one succeeding without the other.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var auditableEntries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in auditableEntries)
        {
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown",
            };
            var module = entry.Entity.GetType().Name;

            context.Set<AuditTrailEntry>().Add(new AuditTrailEntry
            {
                UserId = _currentUser.UserId,
                OfficeId = _currentUser.OfficeId.HasValue ? (int)_currentUser.OfficeId.Value : null,
                Name = $"{action} {module}",
                Module = module,
                Action = action,
                Notes = Summarize(entry),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
    }

    private static string Summarize(EntityEntry entry)
    {
        var changed = entry.Properties
            .Where(p => entry.State == EntityState.Added
                ? p.CurrentValue is not null
                : entry.State == EntityState.Deleted
                    ? p.OriginalValue is not null
                    : !Equals(p.OriginalValue, p.CurrentValue))
            .ToDictionary(
                p => p.Metadata.Name,
                p => entry.State switch
                {
                    EntityState.Deleted => p.OriginalValue,
                    EntityState.Added => p.CurrentValue,
                    _ => new { before = p.OriginalValue, after = p.CurrentValue },
                });

        return JsonSerializer.Serialize(changed);
    }
}
