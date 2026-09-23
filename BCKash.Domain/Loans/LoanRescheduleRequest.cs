using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_reschedule_requests` table (BRD §6.5).</summary>
public class LoanRescheduleRequest : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanId { get; set; }
    public decimal? Principal { get; set; }

    public RescheduleRequestStatus Status { get; set; } = RescheduleRequestStatus.Pending;

    /// <summary>All *_by_id columns below reference `users.id` — outside this table group, kept as plain scalars.</summary>
    public int? CreatedById { get; set; }
    public int? ModifiedById { get; set; }
    public int? ApprovedById { get; set; }
    public int? RejectedById { get; set; }

    public DateOnly? CreatedDate { get; set; }
    public DateOnly? ModifiedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public DateOnly? RejectedDate { get; set; }
    public DateOnly? RescheduleFromDate { get; set; }

    /// <summary>Legacy column is `int(11)` (not `tinyint`), even though it reads as a boolean flag — kept as int per DDL type.</summary>
    public int RecalculateInterest { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
}
