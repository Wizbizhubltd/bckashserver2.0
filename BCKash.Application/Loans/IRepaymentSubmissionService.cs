using BCKash.Domain.Loans;

namespace BCKash.Application.Loans;

public enum RepaymentSubmissionOutcome
{
    Success,
    NotFound,
    InvalidAmount,
    InvalidLoanStatus,

    /// <summary>Together with repayments already waiting for confirmation, more than the client still owes.</summary>
    ExceedsBalance,

    /// <summary>Already approved or disputed.</summary>
    AlreadyReviewed,

    ReasonRequired,

    /// <summary>Only the office's manager (or a super admin) confirms repayments.</summary>
    NotAllowed,

    /// <summary>Approving it failed to post against the loan — see <see cref="RepaymentSubmissionResult.RepaymentOutcome"/>.</summary>
    PostingFailed,
}

public record RepaymentSubmissionResult(
    RepaymentSubmissionOutcome Outcome,
    RepaymentSubmission? Submission = null,
    LoanRepaymentWriteOutcome? RepaymentOutcome = null);

/// <summary>
/// Repayments recorded by staff wait for the office manager to confirm the money arrived. Approving posts
/// the repayment against the loan (and sends the customer's receipt); disputing records why, and it never
/// counts. Staff who confirm repayments themselves — managers, super admins, and users with no user_type
/// (see IOfficeScope) — post straight away instead.
/// </summary>
public interface IRepaymentSubmissionService
{
    /// <summary>Whether a repayment the caller records must wait for confirmation.</summary>
    Task<bool> NeedsConfirmationAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the caller confirms repayments for loans in <paramref name="officeId"/>.</summary>
    Task<bool> CanReviewAsync(int? officeId, CancellationToken cancellationToken = default);

    Task<RepaymentSubmissionResult> SubmitAsync(int loanId, decimal amount, int? paymentTypeId, DateOnly? date, string? notes, CancellationToken cancellationToken = default);

    Task<RepaymentSubmissionResult> ApproveAsync(int submissionId, CancellationToken cancellationToken = default);

    Task<RepaymentSubmissionResult> DisputeAsync(int submissionId, string reason, CancellationToken cancellationToken = default);
}
