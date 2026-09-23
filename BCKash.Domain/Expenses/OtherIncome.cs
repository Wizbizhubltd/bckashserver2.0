using BCKash.SharedKernel;

namespace BCKash.Domain.Expenses;

/// <summary>Maps the legacy `other_income` table (BRD §6.10). Audited per FR-SEC-6 (Phase 8 — approval workflow).</summary>
public class OtherIncome : IHasTimestamps, IAuditable
{
    public int Id { get; set; }
    public int? OfficeId { get; set; }
    public int? CreatedById { get; set; }
    public int? OtherIncomeTypeId { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? Date { get; set; }
    public string? Year { get; set; }
    public string? Month { get; set; }
    public string? Notes { get; set; }
    public string? Files { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Approved;
    public DateOnly? ApprovedDate { get; set; }
    public int? ApprovedById { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public int? DeclinedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public OtherIncomeType? OtherIncomeType { get; set; }
}
