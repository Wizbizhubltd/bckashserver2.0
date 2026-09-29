using BCKash.Api.Contracts;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// The signed-in staff member's bell notifications (see IStaffNotificationService). Opening one marks it
/// read, which also closes it if it was only news; one asking for action stays open until the item is dealt with.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private const int MaxItems = 50;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public NotificationsController(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>What the bell's number shows.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummaryResponse>> Summary(CancellationToken cancellationToken) =>
        Ok(new NotificationSummaryResponse(await Mine().CountAsync(n => n.DoneAt == null, cancellationToken)));

    /// <summary>Newest first: everything still open, then the most recent closed ones, up to 50 in all.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserNotificationResponse>>> List(CancellationToken cancellationToken)
    {
        var open = await Mine().Where(n => n.DoneAt == null).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(MaxItems).ToListAsync(cancellationToken);
        var recent = await Mine().Where(n => n.DoneAt != null).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(MaxItems - open.Count).ToListAsync(cancellationToken);
        return Ok(open.Concat(recent).Select(ToResponse).ToList());
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken cancellationToken)
    {
        var notification = await Mine().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        MarkRead(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(notification));
    }

    /// <summary>Marks every notification read — news closes; anything asking for action stays open until dealt with.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        foreach (var notification in await Mine().Where(n => n.ReadAt == null).ToListAsync(cancellationToken))
        {
            MarkRead(notification);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<UserNotification> Mine() => _db.UserNotifications.Where(n => n.UserId == _currentUser.UserId);

    private static void MarkRead(UserNotification notification)
    {
        var now = DateTime.UtcNow;
        notification.ReadAt ??= now;
        if (!notification.NeedsAction)
        {
            notification.DoneAt ??= now;
        }

        notification.UpdatedAt = now;
    }

    private static UserNotificationResponse ToResponse(UserNotification n) =>
        new(n.Id, n.Kind, n.Title, n.Body, n.Link, n.NeedsAction, n.ReadAt != null, n.DoneAt != null, n.CreatedAt);
}
