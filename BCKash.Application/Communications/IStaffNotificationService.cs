namespace BCKash.Application.Communications;

/// <summary>One bell notification to send. <c>Link</c> is an office-portal path without the role prefix.</summary>
public record StaffNotificationDraft(
    string Kind,
    string Title,
    string? Body,
    string? Link,
    string EntityType,
    int EntityId,
    bool NeedsAction);

/// <summary>
/// The office portal's bell: notifications for directors, controllers, managers and marketers about
/// things in their offices that need them. Whoever performed the action is never notified of it.
/// Sending is best-effort — a failure is logged and never fails the action behind it.
/// </summary>
public interface IStaffNotificationService
{
    /// <summary>
    /// Everyone of <paramref name="userTypes"/> who works in <paramref name="officeId"/>: staff assigned to
    /// it, and directors whose zones include it. Blocked staff are skipped.
    /// </summary>
    Task NotifyOfficeAsync(int? officeId, IReadOnlyCollection<string> userTypes, StaffNotificationDraft draft, CancellationToken cancellationToken = default);

    /// <summary>One staff member (e.g. whoever raised the item that was just reviewed).</summary>
    Task NotifyUserAsync(int? userId, int? officeId, StaffNotificationDraft draft, CancellationToken cancellationToken = default);

    /// <summary>The item was dealt with: every open notification of <paramref name="kind"/> about it closes, for everyone.</summary>
    Task ResolveAsync(string entityType, int entityId, string kind, CancellationToken cancellationToken = default);
}
