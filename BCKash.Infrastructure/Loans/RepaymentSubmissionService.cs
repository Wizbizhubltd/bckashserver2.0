using BCKash.Application.Communications;
using BCKash.Application.Identity;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>See <see cref="IRepaymentSubmissionService"/>.</summary>
public class RepaymentSubmissionService : IRepaymentSubmissionService
{
    private static readonly string[] Reviewers = [UserTypeSlugs.Manager];

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOfficeScope _scope;
    private readonly ILoanRepaymentService _repayments;
    private readonly ILoanNotificationService _customerMessages;
    private readonly IStaffNotificationService _staffNotifications;
    private readonly ICurrencyDisplayProvider _currency;

    public RepaymentSubmissionService(
        BCKashDbContext db,
        ICurrentUserContext currentUser,
        IOfficeScope scope,
        ILoanRepaymentService repayments,
        ILoanNotificationService customerMessages,
        IStaffNotificationService staffNotifications,
        ICurrencyDisplayProvider currency)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _repayments = repayments;
        _customerMessages = customerMessages;
        _staffNotifications = staffNotifications;
        _currency = currency;
    }

    public async Task<bool> NeedsConfirmationAsync(CancellationToken cancellationToken = default) =>
        await _scope.GetUserTypeAsync(cancellationToken) is not (null or UserTypeSlugs.SuperAdmin or UserTypeSlugs.Manager);

    public async Task<bool> CanReviewAsync(int? officeId, CancellationToken cancellationToken = default)
    {
        var userType = await _scope.GetUserTypeAsync(cancellationToken);
        return userType is null or UserTypeSlugs.SuperAdmin
            || (userType == UserTypeSlugs.Manager && await _scope.CanAccessOfficeAsync(officeId, cancellationToken));
    }

    public async Task<RepaymentSubmissionResult> SubmitAsync(int loanId, decimal amount, int? paymentTypeId, DateOnly? date, string? notes, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.InvalidAmount);
        }

        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.NotFound);
        }

        if (loan.Status is LoanStatus.Closed or LoanStatus.Paid)
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.ExceedsBalance);
        }

        if (!LoanTransitionRules.HasActiveSchedule(loan.Status))
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.InvalidLoanStatus);
        }

        // Repayments already waiting will count once confirmed, so they come off what's left to pay.
        var waiting = await _db.RepaymentSubmissions.Where(s => s.LoanId == loanId && s.Status == RepaymentSubmissionStatus.Pending).SumAsync(s => s.Amount, cancellationToken);
        if (amount + waiting > (await _repayments.RemainingAsync(loanId, cancellationToken) ?? 0))
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.ExceedsBalance);
        }

        var submission = new RepaymentSubmission
        {
            LoanId = loanId,
            OfficeId = loan.OfficeId,
            Amount = amount,
            PaymentTypeId = paymentTypeId,
            PaymentDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            SubmittedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.RepaymentSubmissions.Add(submission);
        await _db.SaveChangesAsync(cancellationToken);

        var currency = await _currency.GetAsync(cancellationToken);
        await _staffNotifications.NotifyOfficeAsync(loan.OfficeId, Reviewers, new StaffNotificationDraft(
            UserNotificationKinds.RepaymentPending,
            $"Confirm a repayment of {currency.Format(amount)}",
            $"Recorded for loan {loan.AccountNumber ?? $"#{loan.Id}"} — confirm the money arrived, or dispute it.",
            $"/loans/{loan.Id}?section=repayments",
            NotificationEntities.RepaymentSubmission,
            submission.Id,
            NeedsAction: true), cancellationToken);

        return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.Success, submission);
    }

    public async Task<RepaymentSubmissionResult> ApproveAsync(int submissionId, CancellationToken cancellationToken = default)
    {
        var (submission, problem) = await FindReviewableAsync(submissionId, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var posted = await _repayments.RecordRepaymentAsync(submission!.LoanId, submission.Amount, submission.PaymentTypeId, submission.PaymentDate, submission.Notes, cancellationToken);
        if (posted.Outcome != LoanRepaymentWriteOutcome.Success)
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.PostingFailed, submission, posted.Outcome);
        }

        submission.Status = RepaymentSubmissionStatus.Approved;
        submission.LoanTransactionId = posted.Transaction!.Id;
        await MarkReviewedAsync(submission, cancellationToken);

        // The customer's receipt goes only now the money is confirmed.
        await _customerMessages.PaymentReceivedAsync(submission.LoanId, submission.Amount, submission.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        await TellSubmitterAsync(submission, "confirmed", cancellationToken);
        return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.Success, submission);
    }

    public async Task<RepaymentSubmissionResult> DisputeAsync(int submissionId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.ReasonRequired);
        }

        var (submission, problem) = await FindReviewableAsync(submissionId, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        submission!.Status = RepaymentSubmissionStatus.Disputed;
        submission.DisputeReason = reason.Trim();
        await MarkReviewedAsync(submission, cancellationToken);
        await TellSubmitterAsync(submission, $"disputed: “{submission.DisputeReason}”", cancellationToken);
        return new RepaymentSubmissionResult(RepaymentSubmissionOutcome.Success, submission);
    }

    private async Task<(RepaymentSubmission? Submission, RepaymentSubmissionResult? Problem)> FindReviewableAsync(int submissionId, CancellationToken cancellationToken)
    {
        var submission = await _db.RepaymentSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
        if (submission is null || !await _scope.CanAccessOfficeAsync(submission.OfficeId, cancellationToken))
        {
            return (null, new RepaymentSubmissionResult(RepaymentSubmissionOutcome.NotFound));
        }

        if (!await CanReviewAsync(submission.OfficeId, cancellationToken))
        {
            return (null, new RepaymentSubmissionResult(RepaymentSubmissionOutcome.NotAllowed, submission));
        }

        return submission.Status == RepaymentSubmissionStatus.Pending
            ? (submission, null)
            : (null, new RepaymentSubmissionResult(RepaymentSubmissionOutcome.AlreadyReviewed, submission));
    }

    private async Task MarkReviewedAsync(RepaymentSubmission submission, CancellationToken cancellationToken)
    {
        submission.ReviewedById = _currentUser.UserId;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _staffNotifications.ResolveAsync(NotificationEntities.RepaymentSubmission, submission.Id, UserNotificationKinds.RepaymentPending, cancellationToken);
    }

    private async Task TellSubmitterAsync(RepaymentSubmission submission, string outcome, CancellationToken cancellationToken)
    {
        var currency = await _currency.GetAsync(cancellationToken);
        var loanNumber = await _db.Loans.Where(l => l.Id == submission.LoanId).Select(l => l.AccountNumber).FirstOrDefaultAsync(cancellationToken);
        await _staffNotifications.NotifyUserAsync(submission.SubmittedById, submission.OfficeId, new StaffNotificationDraft(
            UserNotificationKinds.RepaymentReviewed,
            $"Repayment of {currency.Format(submission.Amount)} {outcome}",
            $"Loan {loanNumber ?? $"#{submission.LoanId}"}.",
            $"/loans/{submission.LoanId}?section=repayments",
            NotificationEntities.RepaymentSubmission,
            submission.Id,
            NeedsAction: false), cancellationToken);
    }
}
