using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `collateral_types` table (BRD §6.5).</summary>
public class CollateralType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Collateral> Collaterals { get; set; } = new List<Collateral>();
}
