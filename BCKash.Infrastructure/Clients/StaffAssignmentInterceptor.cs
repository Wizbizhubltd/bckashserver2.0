using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BCKash.Infrastructure.Clients;

/// <summary>
/// Stamps who reassigned a client's or group's marketer (StaffId) and when, however it changed — the
/// group reassign action (whose members follow it), a client transfer, or an edit. The marketer set
/// when the record is created isn't a reassignment, so new records aren't stamped. Registered before
/// the audit interceptor, so the audit trail records the stamp too.
/// </summary>
public class StaffAssignmentInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUser;

    public StaffAssignmentInterceptor(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified && e.Entity is Client or Group))
        {
            var staff = entry.Property(nameof(Client.StaffId));
            if (Equals(staff.OriginalValue, staff.CurrentValue))
            {
                continue;
            }

            entry.Property(nameof(Client.StaffAssignedById)).CurrentValue = _currentUser.UserId;
            entry.Property(nameof(Client.StaffAssignedAt)).CurrentValue = DateTime.UtcNow;
        }
    }
}
