using BCKash.Application.Clients;
using BCKash.Application.Groups;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Groups;

public class GroupMembershipService : IGroupMembershipService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClientAccess _access;

    public GroupMembershipService(BCKashDbContext db, ICurrentUserContext currentUser, IClientAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<MembershipWriteResult> AddAsync(int groupId, int clientId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
        {
            return new MembershipWriteResult(MembershipWriteOutcome.GroupNotFound);
        }

        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken))
        {
            return new MembershipWriteResult(MembershipWriteOutcome.ClientNotFound);
        }

        var alreadyActive = await _db.GroupClients.AnyAsync(
            gc => gc.GroupId == groupId && gc.ClientId == clientId && gc.RemovedAt == null,
            cancellationToken);
        if (alreadyActive)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.AlreadyAMember);
        }

        var membership = new GroupClient
        {
            GroupId = groupId,
            ClientId = clientId,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.GroupClients.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return new MembershipWriteResult(MembershipWriteOutcome.Success, membership);
    }

    public async Task<MembershipWriteResult> RemoveAsync(int groupId, int groupClientId, CancellationToken cancellationToken = default)
    {
        var membership = await _db.GroupClients.FirstOrDefaultAsync(
            gc => gc.Id == groupClientId && gc.GroupId == groupId, cancellationToken);
        if (membership is null || membership.RemovedAt is not null)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.MembershipNotFound);
        }

        membership.RemovedAt = DateTime.UtcNow;
        membership.RemovedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new MembershipWriteResult(MembershipWriteOutcome.Success, membership);
    }

    public async Task<MembershipWriteResult> MoveAsync(int clientId, int groupId, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.GroupNotFound);
        }

        if (group.Status is not (GroupStatus.Pending or GroupStatus.Active))
        {
            return new MembershipWriteResult(MembershipWriteOutcome.GroupNotOpen);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken);
        if (client is null)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.ClientNotFound);
        }

        if (client.OfficeId != group.OfficeId)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.DifferentOffice);
        }

        var current = await _db.GroupClients.Where(gc => gc.ClientId == clientId && gc.RemovedAt == null).ToListAsync(cancellationToken);
        if (current.Any(gc => gc.GroupId == groupId))
        {
            return new MembershipWriteResult(MembershipWriteOutcome.AlreadyAMember);
        }

        if (await IsDefaulterAsync(clientId, cancellationToken))
        {
            return new MembershipWriteResult(MembershipWriteOutcome.Defaulter);
        }

        if (await _access.ActiveLoanAsync(clientId, cancellationToken) is not null)
        {
            return new MembershipWriteResult(MembershipWriteOutcome.HasActiveLoan);
        }

        var now = DateTime.UtcNow;
        foreach (var membership in current)
        {
            membership.RemovedAt = now;
            membership.RemovedById = _currentUser.UserId;
        }

        // Joins in the next free role: leader, assistant and organizer before plain members.
        var memberCount = await _db.GroupClients.CountAsync(gc => gc.GroupId == groupId && gc.RemovedAt == null, cancellationToken);
        var moved = new GroupClient
        {
            GroupId = groupId,
            ClientId = clientId,
            Role = GroupMemberRoles.ForPosition(memberCount),
            CreatedById = _currentUser.UserId,
            CreatedAt = now,
        };
        _db.GroupClients.Add(moved);
        await _db.SaveChangesAsync(cancellationToken);

        return new MembershipWriteResult(MembershipWriteOutcome.Success, moved);
    }

    /// <summary>A loan of theirs, or their share of a group loan, is non-performing or written off.</summary>
    private Task<bool> IsDefaulterAsync(int clientId, CancellationToken cancellationToken)
    {
        var allocatedLoanIds = _db.GroupLoanAllocations.Where(a => a.ClientId == clientId && a.LoanId != null).Select(a => a.LoanId!.Value);
        return _db.Loans.AnyAsync(
            l => l.DeletedAt == null
                && (l.ClientId == clientId || allocatedLoanIds.Contains(l.Id))
                && (l.IsNpa || l.Status == LoanStatus.WrittenOff),
            cancellationToken);
    }
}
