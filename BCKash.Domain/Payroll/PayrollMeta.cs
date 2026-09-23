using BCKash.SharedKernel;

namespace BCKash.Domain.Payroll;

/// <summary>Maps the legacy `payroll_meta` table — per-payroll addition/deduction line items (BRD §6.8).</summary>
public class PayrollMeta : IHasTimestamps
{
    public int Id { get; set; }
    public int PayrollId { get; set; }
    public int? PayrollTemplateMetaId { get; set; }
    public decimal? Value { get; set; }
    public bool? IsTax { get; set; }
    public bool? IsPercentage { get; set; }
    public PayrollMetaPosition? Position { get; set; } = PayrollMetaPosition.BottomLeft;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Payroll Payroll { get; set; } = null!;
    public PayrollTemplateMeta? PayrollTemplateMeta { get; set; }
}
