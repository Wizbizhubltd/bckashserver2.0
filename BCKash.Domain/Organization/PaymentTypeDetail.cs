using BCKash.SharedKernel;

namespace BCKash.Domain.Organization;

/// <summary>Maps the legacy `payment_type_details` table — BRD §6.1.</summary>
public class PaymentTypeDetail : IHasTimestamps
{
    public int Id { get; set; }
    public PaymentTypeDetailReferenceType? Type { get; set; }
    public int ReferenceId { get; set; }
    public string? AccountNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? RoutingCode { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Bank { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
