using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>
/// Maps the legacy `collateral` table (BRD §6.5). <see cref="LoanApplicationId"/> is new in
/// Phase 4 — the legacy schema only had <see cref="LoanId"/>, but the phase's own scope/acceptance
/// criteria expect collateral (like <see cref="Guarantor"/>, which already has both FKs) to be
/// capturable at the application stage, before a Loan exists.
/// </summary>
public class Collateral : IHasTimestamps
{
    public int Id { get; set; }

    public int? LoanId { get; set; }
    public int? LoanApplicationId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    public int? CollateralTypeId { get; set; }

    public string? Name { get; set; }
    public string? Serial { get; set; }
    public decimal? Value { get; set; }
    public string? Description { get; set; }
    public string? Picture { get; set; }
    public string? Gallery { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Loan? Loan { get; set; }
    public LoanApplication? LoanApplication { get; set; }
    public CollateralType? CollateralType { get; set; }
}
