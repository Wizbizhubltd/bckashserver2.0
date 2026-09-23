using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `currencies` table — BRD §6.1.</summary>
public class Currency : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Symbol { get; set; }
    public string? Decimals { get; set; } = "2";
    public decimal? Xrate { get; set; }
    public string? InternationalCode { get; set; }
    public bool Active { get; set; } = true;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
