using BCKash.SharedKernel;

namespace BCKash.Domain.Payroll;

/// <summary>Maps the legacy `payroll` table — BRD §6.8. Audited per FR-SEC-6.</summary>
public class Payroll : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? PayrollTemplateId { get; set; }
    public int? GlAccountExpenseId { get; set; }
    public int? GlAccountAssetId { get; set; }
    public int? UserId { get; set; }
    public int? OfficeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? BusinessName { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTypeId { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Description { get; set; }
    public string? Comments { get; set; }

    /// <summary>
    /// New in Phase 8 — the legacy schema only stores the computed <see cref="PaidAmount"/>;
    /// the gross amount a run was computed from is kept alongside it so the net-pay
    /// computation stays auditable/reproducible without re-deriving it from the meta rows.
    /// </summary>
    public decimal GrossAmount { get; set; }

    public decimal PaidAmount { get; set; }
    public DateOnly? Date { get; set; }
    public string? Year { get; set; }
    public string? Month { get; set; }
    public bool Recurring { get; set; }
    public string RecurFrequency { get; set; } = "31";
    public DateOnly? RecurStartDate { get; set; }
    public DateOnly? RecurEndDate { get; set; }
    public DateOnly? RecurNextDate { get; set; }
    public PayrollRecurType RecurType { get; set; } = PayrollRecurType.Months;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public PayrollTemplate? PayrollTemplate { get; set; }
}
