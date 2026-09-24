using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>An operational grouping of offices. Managed by super admins only.</summary>
public class Zone : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CreatedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
