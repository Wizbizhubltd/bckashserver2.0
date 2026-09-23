using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `payment_details` table — BRD §6.1.</summary>
public class PaymentDetail : IHasTimestamps
{
    public int Id { get; set; }
    public int? PaymentTypeId { get; set; }
    public string? AccountNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? RoutingCode { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Bank { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public PaymentType? PaymentType { get; set; }
}
