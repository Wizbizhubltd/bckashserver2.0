using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>
/// Maps the legacy `loan_transaction_repayment_schedule_mappings` table (BRD §6.5).
/// Despite the name this is not a pure composite-key join table — it has its own
/// surrogate `id` primary key plus allocation amount columns.
/// </summary>
public class LoanTransactionRepaymentScheduleMapping : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanRepaymentScheduleId { get; set; }
    public int? LoanTransactionId { get; set; }

    public decimal? Interest { get; set; }
    public decimal? Principal { get; set; }
    public decimal? Fee { get; set; }
    public decimal? Penalty { get; set; }
    public decimal? Overpayment { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public LoanRepaymentSchedule? LoanRepaymentSchedule { get; set; }
    public LoanTransaction? LoanTransaction { get; set; }
}
