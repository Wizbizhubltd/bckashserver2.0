using BCKash.SharedKernel;

namespace BCKash.Domain.Communications;

/// <summary>Maps the legacy `sms_gateways` table — configured outbound SMS gateway providers (BRD §6.11).</summary>
public class SmsGateway : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public string? Name { get; set; }
    public string? FromName { get; set; }
    public string? ToName { get; set; }
    public string? Url { get; set; }
    public string? MsgName { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
