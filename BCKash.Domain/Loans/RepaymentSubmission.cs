using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

public enum RepaymentSubmissionStatus
{
    /// <summary>Recorded by staff; not yet counted against the loan.</summary>
    Pending,

    /// <summary>The office manager confirmed the money arrived — it was then posted as a repayment (<see cref="RepaymentSubmission.LoanTransactionId"/>).</summary>
    Approved,

    /// <summary>The office manager says the money wasn't received as described. It never counts.</summary>
    Disputed,
}

/// <summary>
/// A repayment staff have recorded, waiting for the office manager to confirm the money arrived. Only
/// once approved is it posted against the loan's schedule, and only then does the customer get a
/// receipt. New — no legacy table.
/// </summary>
public class RepaymentSubmission : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int? OfficeId { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentTypeId { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public string? Notes { get; set; }
    public RepaymentSubmissionStatus Status { get; set; } = RepaymentSubmissionStatus.Pending;
    public int? SubmittedById { get; set; }
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DisputeReason { get; set; }

    /// <summary>The repayment it became once approved.</summary>
    public int? LoanTransactionId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
