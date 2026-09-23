using BCKash.SharedKernel;

namespace BCKash.Domain.Payroll;

/// <summary>Maps the legacy `payroll_templates` table (BRD §6.8).</summary>
public class PayrollTemplate : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? Picture { get; set; }
    public bool? Active { get; set; } = true;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PayrollTemplateMeta> PayrollTemplateMeta { get; set; } = new List<PayrollTemplateMeta>();
    public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
}
