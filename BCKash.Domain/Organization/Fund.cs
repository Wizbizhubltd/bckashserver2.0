using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `funds` table — BRD §6.1.</summary>
public class Fund : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
