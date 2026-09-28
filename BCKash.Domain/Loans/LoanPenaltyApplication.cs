namespace BCKash.Domain.Loans;

/// <summary>
/// One automatically charged penalty occurrence — the idempotency record for the daily penalty run
/// (unique per charge, loan, instalment and occurrence), so re-running never charges twice.
/// New (not in the legacy schema).
/// </summary>
public class LoanPenaltyApplication
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int ChargeId { get; set; }

    /// <summary>The instalment a late fee is for; 0 for a loan-level default penalty.</summary>
    public int ScheduleId { get; set; }

    public int Occurrence { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public int? LoanChargeId { get; set; }
    public int? LoanTransactionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
