using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>User-facing reminders (BR-COM-5, FR-COM-4) — always scoped to the current user; no permission gate beyond being authenticated.</summary>
[ApiController]
[Route("api/reminders")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly BCKashDbContext _db;
    private readonly IReminderService _reminderService;
    private readonly ICurrentUserContext _currentUser;

    public RemindersController(BCKashDbContext db, IReminderService reminderService, ICurrentUserContext currentUser)
    {
        _db = db;
        _reminderService = reminderService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderResponse>>> List([FromQuery] bool? completed, CancellationToken cancellationToken)
    {
        var query = _db.Reminders.Where(r => r.UserId == _currentUser.UserId);
        if (completed.HasValue)
        {
            query = query.Where(r => r.Completed == completed);
        }

        var reminders = await query.OrderByDescending(r => r.Id).ToListAsync(cancellationToken);
        return Ok(reminders.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateReminderRequest request, CancellationToken cancellationToken)
    {
        var result = await _reminderService.CreateAsync(request.Code, cancellationToken);
        return CreatedAtAction(nameof(List), null, ToResponse(result.Reminder!));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        var result = await _reminderService.CompleteAsync(id, cancellationToken);
        return result.Outcome switch
        {
            ReminderWriteOutcome.Success => Ok(ToResponse(result.Reminder!)),
            ReminderWriteOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static ReminderResponse ToResponse(Reminder r) => new(r.Id, r.UserId, r.Code, r.Completed, r.CompletedAt);
}
