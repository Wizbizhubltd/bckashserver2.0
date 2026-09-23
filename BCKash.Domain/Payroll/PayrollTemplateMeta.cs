using BCKash.SharedKernel;

namespace BCKash.Domain.Payroll;

/// <summary>Maps the legacy `payroll_template_meta` table — the addition/deduction line definitions on a payroll template (BRD §6.8).</summary>
public class PayrollTemplateMeta : IHasTimestamps
{
    public int Id { get; set; }
    public int PayrollTemplateId { get; set; }
    public string? Name { get; set; }
    public PayrollTemplateMetaPosition? Position { get; set; } = PayrollTemplateMetaPosition.BottomLeft;
    public PayrollTemplateMetaType? Type { get; set; } = PayrollTemplateMetaType.Addition;
    public bool IsDefault { get; set; }
    public bool IsTax { get; set; }
    public bool IsPercentage { get; set; }
    public PayrollTemplateMetaTaxOn? TaxOn { get; set; } = PayrollTemplateMetaTaxOn.Net;
    public decimal? DefaultValue { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public PayrollTemplate PayrollTemplate { get; set; } = null!;
}
