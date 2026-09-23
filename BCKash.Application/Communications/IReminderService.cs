using BCKash.Domain.Communications;

namespace BCKash.Application.Communications;

public enum ReminderWriteOutcome
{
    Success,
    NotFound,
}

public record ReminderWriteResult(ReminderWriteOutcome Outcome, Reminder? Reminder = null);

/// <summary>User-facing reminders/notifications (BR-COM-5, FR-COM-4) — always scoped to the current user.</summary>
public interface IReminderService
{
    Task<ReminderWriteResult> CreateAsync(string code, CancellationToken cancellationToken = default);

    Task<ReminderWriteResult> CompleteAsync(int id, CancellationToken cancellationToken = default);
}
