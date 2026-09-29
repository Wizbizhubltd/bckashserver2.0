using BCKash.SharedKernel;

namespace BCKash.Domain.Identity;

/// <summary>
/// A staff member's bell notification in the office portal. Something that needs their action (a client
/// to approve, a repayment to confirm…) stays open until anyone deals with the item it's about; something
/// they only need to know (their client was approved…) closes once they open it. New — no legacy table.
/// </summary>
public class UserNotification : IHasTimestamps
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? OfficeId { get; set; }

    /// <summary>What happened — see <see cref="UserNotificationKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>Where it opens in the office portal, e.g. "/loans/12?section=repayments" (the portal adds the role prefix).</summary>
    public string? Link { get; set; }

    /// <summary>The record it's about, so dealing with that record closes the notification for everyone.</summary>
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }

    /// <summary>True when it asks the recipient to do something, rather than just telling them.</summary>
    public bool NeedsAction { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>Set once it no longer counts on the bell: the item was dealt with, or (for news) it was read.</summary>
    public DateTime? DoneAt { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class UserNotificationKinds
{
    public const string ClientPendingApproval = "client_pending_approval";
    public const string ClientReviewed = "client_reviewed";
    public const string EditRequestPending = "edit_request_pending";
    public const string EditRequestReviewed = "edit_request_reviewed";
    public const string LoanApplicationPending = "loan_application_pending";
    public const string LoanApplicationReviewed = "loan_application_reviewed";
    public const string LoanAwaitingDisbursement = "loan_awaiting_disbursement";
    public const string RepaymentPending = "repayment_pending";
    public const string RepaymentReviewed = "repayment_reviewed";
}

/// <summary>The records a notification can be about.</summary>
public static class NotificationEntities
{
    public const string Client = "client";
    public const string EditRequest = "client_edit_request";
    public const string LoanApplication = "loan_application";
    public const string Loan = "loan";
    public const string RepaymentSubmission = "repayment_submission";
}
