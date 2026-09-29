using BCKash.Application.Communications;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCKash.Infrastructure.Communications;

/// <summary>See <see cref="IStaffNotificationService"/>.</summary>
public class StaffNotificationService : IStaffNotificationService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<StaffNotificationService> _logger;

    public StaffNotificationService(BCKashDbContext db, ICurrentUserContext currentUser, ILogger<StaffNotificationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task NotifyOfficeAsync(int? officeId, IReadOnlyCollection<string> userTypes, StaffNotificationDraft draft, CancellationToken cancellationToken = default)
    {
        if (officeId is null)
        {
            return;
        }

        try
        {
            var typed = _db.RoleUsers.Where(ru => userTypes.Contains(ru.Role.Slug));
            var zoneId = await _db.Offices.Where(o => o.Id == officeId).Select(o => o.ZoneId).FirstOrDefaultAsync(cancellationToken);

            // Directors work in every office of their zones; everyone else in the office they're assigned to.
            var recipients = await _db.Users
                .Where(u => !u.Blocked && typed.Any(ru => ru.UserId == u.Id))
                .Where(u => (u.OfficeId == officeId && !_db.RoleUsers.Any(ru => ru.UserId == u.Id && ru.Role.Slug == UserTypeSlugs.Director))
                         || (zoneId != null && _db.RoleUsers.Any(ru => ru.UserId == u.Id && ru.Role.Slug == UserTypeSlugs.Director)
                                            && _db.UserZones.Any(uz => uz.UserId == u.Id && uz.ZoneId == zoneId)))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            await AddAsync(recipients, officeId, draft, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Couldn't send the {Kind} notification for {EntityType} {EntityId}.", draft.Kind, draft.EntityType, draft.EntityId);
        }
    }

    public async Task NotifyUserAsync(int? userId, int? officeId, StaffNotificationDraft draft, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return;
        }

        try
        {
            await AddAsync([userId.Value], officeId, draft, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Couldn't send the {Kind} notification for {EntityType} {EntityId}.", draft.Kind, draft.EntityType, draft.EntityId);
        }
    }

    public async Task ResolveAsync(string entityType, int entityId, string kind, CancellationToken cancellationToken = default)
    {
        try
        {
            var open = await _db.UserNotifications
                .Where(n => n.EntityType == entityType && n.EntityId == entityId && n.Kind == kind && n.DoneAt == null)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var notification in open)
            {
                notification.DoneAt = now;
                notification.UpdatedAt = now;
            }

            if (open.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Couldn't close the {Kind} notifications for {EntityType} {EntityId}.", kind, entityType, entityId);
        }
    }

    /// <summary>Whoever performed the action doesn't need telling about it.</summary>
    private async Task AddAsync(IEnumerable<int> userIds, int? officeId, StaffNotificationDraft draft, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var added = false;
        foreach (var userId in userIds.Distinct().Where(id => id != _currentUser.UserId))
        {
            _db.UserNotifications.Add(new UserNotification
            {
                UserId = userId,
                OfficeId = officeId,
                Kind = draft.Kind,
                Title = draft.Title,
                Body = draft.Body,
                Link = draft.Link,
                EntityType = draft.EntityType,
                EntityId = draft.EntityId,
                NeedsAction = draft.NeedsAction,
                CreatedAt = now,
                UpdatedAt = now,
            });
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
