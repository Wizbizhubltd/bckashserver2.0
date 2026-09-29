namespace BCKash.Api.Contracts;

/// <summary>
/// A bell notification. <c>Link</c> is an office-portal path without the role prefix. <c>Done</c>: it no longer
/// counts on the bell — the item was dealt with, or (for news) it was read.
/// </summary>
public record UserNotificationResponse(
    int Id, string Kind, string Title, string? Body, string? Link, bool NeedsAction, bool Read, bool Done, DateTime? CreatedAt);

/// <summary>What the bell shows: how many notifications still count.</summary>
public record NotificationSummaryResponse(int OpenCount);
