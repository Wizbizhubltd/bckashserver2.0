using BCKash.SharedKernel;

namespace BCKash.Domain.GeneralLedger;

/// <summary>
/// Maps the legacy `office_transactions` table (BRD §6.7) — a fund transfer between
/// two offices. `from_office_id`/`to_office_id`/`currency_id` reference tables outside
/// this table group, so they stay plain scalar columns; no GL-posting logic here.
/// </summary>
public class OfficeTransaction : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? FromOfficeId { get; set; }
    public int? ToOfficeId { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? Date { get; set; }
    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
