using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>A city or town within an <see cref="Lga"/>. Unlike states and LGAs there's no official list, so super admins add these.</summary>
public class City : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int LgaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? CreatedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Lga Lga { get; set; } = null!;
}
