using BCKash.Application.Groups;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Groups;

public class GroupService : IGroupService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public GroupService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GroupWriteResult> CreateAsync(Group group, CancellationToken cancellationToken = default)
    {
        _db.Groups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> UpdateAsync(int id, Group updated, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        group.OfficeId = updated.OfficeId;
        group.Name = updated.Name;
        group.ExternalId = updated.ExternalId;
        group.StaffId = updated.StaffId;
        group.JoinedDate = updated.JoinedDate;
        group.Mobile = updated.Mobile;
        group.Phone = updated.Phone;
        group.Email = updated.Email;
        group.Street = updated.Street;
        group.Ward = updated.Ward;
        group.District = updated.District;
        group.Region = updated.Region;
        group.Address = updated.Address;
        group.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> ActivateAsync(int id, DateOnly? activatedDate, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        if (!GroupStatusTransitionRules.CanActivate(group.Status))
        {
            return new GroupWriteResult(GroupWriteOutcome.InvalidTransition);
        }

        group.Status = GroupStatus.Active;
        group.ActivatedDate = activatedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        group.ActivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new GroupWriteResult(GroupWriteOutcome.ReasonRequired);
        }

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        if (!GroupStatusTransitionRules.CanDeactivate(group.Status))
        {
            return new GroupWriteResult(GroupWriteOutcome.InvalidTransition);
        }

        group.Status = GroupStatus.Inactive;
        group.InactiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        group.InactiveById = _currentUser.UserId;
        group.InactiveReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> ReactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        if (!GroupStatusTransitionRules.CanReactivate(group.Status))
        {
            return new GroupWriteResult(GroupWriteOutcome.InvalidTransition);
        }

        group.Status = GroupStatus.Active;
        group.ReactivatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        group.ReactivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new GroupWriteResult(GroupWriteOutcome.ReasonRequired);
        }

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        if (!GroupStatusTransitionRules.CanDecline(group.Status))
        {
            return new GroupWriteResult(GroupWriteOutcome.InvalidTransition);
        }

        group.Status = GroupStatus.Declined;
        group.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        group.DeclinedById = _currentUser.UserId;
        group.DeclinedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> CloseAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new GroupWriteResult(GroupWriteOutcome.ReasonRequired);
        }

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        if (!GroupStatusTransitionRules.CanClose(group.Status))
        {
            return new GroupWriteResult(GroupWriteOutcome.InvalidTransition);
        }

        group.Status = GroupStatus.Closed;
        group.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        group.ClosedById = _currentUser.UserId;
        group.ClosedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }

    public async Task<GroupWriteResult> ReassignMarketerAsync(int id, int marketerId, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.NotFound);
        }

        var marketer = await _db.Users
            .Where(u => u.Id == marketerId && !u.Blocked && _db.RoleUsers.Any(ru => ru.UserId == u.Id && ru.Role.Slug == UserTypeSlugs.Marketer))
            .Select(u => new { u.Id, u.OfficeId })
            .FirstOrDefaultAsync(cancellationToken);
        if (marketer is null)
        {
            return new GroupWriteResult(GroupWriteOutcome.MarketerNotFound);
        }

        if (group.OfficeId.HasValue && marketer.OfficeId != group.OfficeId)
        {
            return new GroupWriteResult(GroupWriteOutcome.MarketerInDifferentOffice);
        }

        if (group.StaffId == marketerId)
        {
            return new GroupWriteResult(GroupWriteOutcome.AlreadyAssigned);
        }

        group.StaffId = marketerId;

        // The group's current members follow it, so the new marketer can see and serve them.
        var memberClientIds = _db.GroupClients.Where(gc => gc.GroupId == id && gc.RemovedAt == null && gc.ClientId != null).Select(gc => gc.ClientId!.Value);
        foreach (var client in await _db.Clients.Where(c => memberClientIds.Contains(c.Id) && c.DeletedAt == null).ToListAsync(cancellationToken))
        {
            client.StaffId = marketerId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new GroupWriteResult(GroupWriteOutcome.Success, group);
    }
}
