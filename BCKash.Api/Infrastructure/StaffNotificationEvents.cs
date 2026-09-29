using BCKash.Application.Communications;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;

namespace BCKash.Api.Infrastructure;

/// <summary>
/// The office portal's bell, event by event: who in the office each one goes to, and what it says.
/// Something needing action goes to the roles that can act on it and closes for all of them once it's
/// dealt with; news of the outcome goes to whoever raised the item.
/// </summary>
public static class StaffNotificationEvents
{
    private static readonly string[] Controllers = [UserTypeSlugs.Controller];
    private static readonly string[] LoanApprovers = [UserTypeSlugs.Controller, UserTypeSlugs.Director];
    private static readonly string[] Disbursers = [UserTypeSlugs.Manager, UserTypeSlugs.Controller];

    public static string NameOf(Client client) =>
        $"{client.FirstName} {client.LastName}".Trim() is { Length: > 0 } name ? name : client.DisplayName ?? client.AccountNo ?? $"Client #{client.Id}";

    /// <summary>A client is waiting for a controller: newly onboarded, or sent back to pending by an edit.</summary>
    public static Task ClientPendingAsync(this IStaffNotificationService notifications, Client client, bool afterEdit, CancellationToken cancellationToken) =>
        notifications.NotifyOfficeAsync(client.OfficeId, Controllers, new StaffNotificationDraft(
            UserNotificationKinds.ClientPendingApproval,
            $"Approve {NameOf(client)}",
            afterEdit ? "Their profile was edited, so they're pending approval again." : "Newly onboarded and pending approval once their documentation is complete.",
            $"/clients/{client.Id}",
            NotificationEntities.Client,
            client.Id,
            NeedsAction: true), cancellationToken);

    /// <summary>A controller approved or declined the client: the approval request closes, and whoever onboarded them hears.</summary>
    public static async Task ClientReviewedAsync(this IStaffNotificationService notifications, Client client, bool approved, CancellationToken cancellationToken)
    {
        await notifications.ResolveAsync(NotificationEntities.Client, client.Id, UserNotificationKinds.ClientPendingApproval, cancellationToken);
        await notifications.NotifyUserAsync(client.CreatedById, client.OfficeId, new StaffNotificationDraft(
            UserNotificationKinds.ClientReviewed,
            $"{NameOf(client)} was {(approved ? "approved" : "declined")}",
            approved ? "They can now apply for a loan." : client.DeclinedReason is { Length: > 0 } reason ? $"Reason: {reason}" : null,
            $"/clients/{client.Id}",
            NotificationEntities.Client,
            client.Id,
            NeedsAction: false), cancellationToken);
    }

    /// <summary>The client is gone, so nothing about them is waiting any more.</summary>
    public static Task ClientRemovedAsync(this IStaffNotificationService notifications, int clientId, CancellationToken cancellationToken) =>
        notifications.ResolveAsync(NotificationEntities.Client, clientId, UserNotificationKinds.ClientPendingApproval, cancellationToken);

    public static Task EditRequestRaisedAsync(this IStaffNotificationService notifications, ClientEditRequest request, Client client, CancellationToken cancellationToken) =>
        notifications.NotifyOfficeAsync(client.OfficeId, Controllers, new StaffNotificationDraft(
            UserNotificationKinds.EditRequestPending,
            $"Edit request for {NameOf(client)}",
            $"“{request.Reason}”",
            $"/clients/{client.Id}",
            NotificationEntities.EditRequest,
            request.Id,
            NeedsAction: true), cancellationToken);

    public static async Task EditRequestReviewedAsync(this IStaffNotificationService notifications, ClientEditRequest request, Client client, bool granted, CancellationToken cancellationToken)
    {
        await notifications.ResolveAsync(NotificationEntities.EditRequest, request.Id, UserNotificationKinds.EditRequestPending, cancellationToken);
        await notifications.NotifyUserAsync(request.RequestedById, client.OfficeId, new StaffNotificationDraft(
            UserNotificationKinds.EditRequestReviewed,
            $"Edit request for {NameOf(client)} {(granted ? "granted" : "refused")}",
            granted ? "You can now edit their profile." : request.ReviewNote is { Length: > 0 } note ? $"Reason: {note}" : null,
            $"/clients/{client.Id}",
            NotificationEntities.EditRequest,
            request.Id,
            NeedsAction: false), cancellationToken);
    }

    public static Task LoanApplicationRaisedAsync(this IStaffNotificationService notifications, LoanApplication application, string applicant, string amount, CancellationToken cancellationToken) =>
        notifications.NotifyOfficeAsync(application.OfficeId, LoanApprovers, new StaffNotificationDraft(
            UserNotificationKinds.LoanApplicationPending,
            $"Loan application from {applicant}",
            $"{amount} — waiting for approval.",
            $"/loan-applications/{application.Id}",
            NotificationEntities.LoanApplication,
            application.Id,
            NeedsAction: true), cancellationToken);

    /// <summary>
    /// The application was approved or declined: the approval request closes, whoever raised it hears, and an
    /// approved loan now waits for disbursement.
    /// </summary>
    public static async Task LoanApplicationReviewedAsync(
        this IStaffNotificationService notifications, LoanApplication application, Loan? loan, string applicant, bool approved, CancellationToken cancellationToken)
    {
        await notifications.ResolveAsync(NotificationEntities.LoanApplication, application.Id, UserNotificationKinds.LoanApplicationPending, cancellationToken);
        await notifications.NotifyUserAsync(application.UserId, application.OfficeId, new StaffNotificationDraft(
            UserNotificationKinds.LoanApplicationReviewed,
            $"Loan application for {applicant} {(approved ? "approved" : "declined")}",
            approved ? $"Loan {loan?.AccountNumber} is ready for disbursement." : application.DeclinedNotes is { Length: > 0 } reason ? $"Reason: {reason}" : null,
            approved && loan is not null ? $"/loans/{loan.Id}" : $"/loan-applications/{application.Id}",
            NotificationEntities.LoanApplication,
            application.Id,
            NeedsAction: false), cancellationToken);

        if (approved && loan is not null)
        {
            await notifications.NotifyOfficeAsync(loan.OfficeId, Disbursers, new StaffNotificationDraft(
                UserNotificationKinds.LoanAwaitingDisbursement,
                $"Disburse loan {loan.AccountNumber} to {applicant}",
                "Approved — disburse once each client receiving money has passed their face match.",
                $"/loans/{loan.Id}",
                NotificationEntities.Loan,
                loan.Id,
                NeedsAction: true), cancellationToken);
        }
    }

    public static Task LoanDisbursedAsync(this IStaffNotificationService notifications, int loanId, CancellationToken cancellationToken) =>
        notifications.ResolveAsync(NotificationEntities.Loan, loanId, UserNotificationKinds.LoanAwaitingDisbursement, cancellationToken);
}
