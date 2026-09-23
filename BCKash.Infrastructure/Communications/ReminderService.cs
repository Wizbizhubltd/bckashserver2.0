using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Communications;

public class ReminderService : IReminderService
{
    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ReminderService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReminderWriteResult> CreateAsync(string code, CancellationToken cancellationToken = default)
    {
        var reminder = new Reminder { UserId = _currentUser.UserId ?? 0, Code = code };
        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReminderWriteResult(ReminderWriteOutcome.Success, reminder);
    }

    public async Task<ReminderWriteResult> CompleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var reminder = await _db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.UserId == _currentUser.UserId, cancellationToken);
        if (reminder is null)
        {
            return new ReminderWriteResult(ReminderWriteOutcome.NotFound);
        }

        reminder.Completed = true;
        reminder.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ReminderWriteResult(ReminderWriteOutcome.Success, reminder);
    }
}
