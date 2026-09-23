using BCKash.SharedKernel;

namespace BCKash.Domain.Expenses;

/// <summary>Maps the legacy `expenses` table (BRD §6.10). Audited per FR-SEC-6 (Phase 8 — approval workflow).</summary>
public class Expense : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? OfficeId { get; set; }
    public int? CreatedById { get; set; }
    public int? ExpenseTypeId { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? Date { get; set; }
    public string? Year { get; set; }
    public string? Month { get; set; }
    public bool Recurring { get; set; }
    public string RecurFrequency { get; set; } = "31";
    public DateOnly? RecurStartDate { get; set; }
    public DateOnly? RecurEndDate { get; set; }
    public DateOnly? RecurNextDate { get; set; }
    public ExpenseRecurType RecurType { get; set; } = ExpenseRecurType.Month;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Approved;
    public DateOnly? ApprovedDate { get; set; }
    public int? ApprovedById { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public int? DeclinedById { get; set; }
    public string? Notes { get; set; }
    public string? Files { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ExpenseType? ExpenseType { get; set; }
}
