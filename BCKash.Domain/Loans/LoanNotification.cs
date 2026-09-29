using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>
/// A reminder sent to a customer about their loan — kept so each reminder goes only once: per
/// instalment for upcoming and missed repayments (<see cref="ScheduleId"/> set), per loan for the
/// loan-overdue notice. New — the legacy schema has no such table.
/// </summary>
public class LoanNotification : IHasTimestamps
{
    public const string UpcomingRepaymentKind = "upcoming_repayment";
    public const string MissedRepaymentKind = "missed_repayment";
    public const string LoanOverdueKind = "loan_overdue";

    public int Id { get; set; }
    public int LoanId { get; set; }

    /// <summary>The instalment the reminder was about; null for the loan-overdue notice.</summary>
    public int? ScheduleId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
