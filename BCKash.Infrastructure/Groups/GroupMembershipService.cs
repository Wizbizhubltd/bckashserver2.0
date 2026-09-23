using BCKash.Application.Groups;
using BCKash.Domain.Groups;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Groups;

public class GroupMembershipService : IGroupMembershipService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public GroupMembershipService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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
}
