using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `payment_types` table — BRD §6.1.</summary>
public class PaymentType : IHasTimestamps
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsCash { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PaymentDetail> PaymentDetails { get; set; } = new List<PaymentDetail>();
}
