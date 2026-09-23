using BCKash.SharedKernel;

namespace BCKash.Domain.Communications;

/// <summary>
/// Maps the legacy `communication_campaigns` table — automated SMS/email campaigns (BRD §6.11).
/// Several columns that look like foreign keys (`office_id`, `loan_officer_id`, `gl_account_id`,
/// `loan_product_id`) are legacy `varchar(191)` free-text filter values, not integer FKs — kept
/// as plain strings to match the DDL exactly.
/// </summary>
public class CommunicationCampaign : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public CampaignType? Type { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateOnly? ReportStartDate { get; set; }
    public string? ReportStartTime { get; set; }
    public CampaignRecurrenceType? RecurrenceType { get; set; }
    public CampaignRecurFrequency? RecurFrequency { get; set; }
    public string? RecurInterval { get; set; }
    public string? EmailRecipients { get; set; }
    public string? EmailSubject { get; set; }
    public string? Message { get; set; }
    public CampaignFileFormat? EmailAttachmentFileFormat { get; set; }
    public CampaignRecipientsCategory? RecipientsCategory { get; set; }
    public CampaignReportAttachment? ReportAttachment { get; set; }
    public string? FromDay { get; set; }
    public string? ToDay { get; set; }
    public string? OfficeId { get; set; }
    public string? LoanOfficerId { get; set; }
    public string? GlAccountId { get; set; }
    public string? ManualEntries { get; set; }
    public string? LoanStatus { get; set; }
    public string? LoanProductId { get; set; }
    public DateOnly? LastRunDate { get; set; }
    public DateOnly? NextRunDate { get; set; }
    public DateOnly? LastRunTime { get; set; }
    public DateOnly? NextRunTime { get; set; }
    public int NumberOfRuns { get; set; }
    public int NumberOfRecipients { get; set; }
    public bool Active { get; set; } = true;
    public bool Sent { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Pending;
    public int? ApprovedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
