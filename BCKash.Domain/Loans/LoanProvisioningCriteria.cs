using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loan_provisioning_criteria` table (BRD §6.5).</summary>
public class LoanProvisioningCriteria : IHasTimestamps
{
    public int Id { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CreatedById { get; set; }

    public string? Name { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? Percentage { get; set; }

    /// <summary>References `gl_accounts.id` — outside this table group, kept as a plain scalar.</summary>
    public int? GlAccountLiabilityId { get; set; }

    /// <summary>References `gl_accounts.id` — outside this table group, kept as a plain scalar.</summary>
    public int? GlAccountExpenseId { get; set; }

    public string? Notes { get; set; }
    public bool Active { get; set; } = true;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
