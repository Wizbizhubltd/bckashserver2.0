using BCKash.SharedKernel;

namespace BCKash.Domain.Reporting;

/// <summary>
/// Maps the legacy `report_scheduler` table — scheduled/recurring report generation and delivery
/// (BRD §6.12). Several columns that look like foreign keys (`office_id`, `loan_officer_id`,
/// `gl_account_id`, `loan_product_id`) are legacy `varchar(191)` free-text filter values, not
/// integer FKs — kept as plain strings to match the DDL exactly.
/// </summary>
public class ReportScheduler : IHasTimestamps
{
    public int Id { get; set; }
    public int? CreatedById { get; set; }
    public string? Description { get; set; }
    public DateOnly? ReportStartDate { get; set; }
    public string? ReportStartTime { get; set; }
    public ReportSchedulerRecurrenceType? RecurrenceType { get; set; }
    public ReportSchedulerRecurFrequency? RecurFrequency { get; set; }
    public string? RecurInterval { get; set; }
    public string? EmailRecipients { get; set; }
    public string? EmailSubject { get; set; }
    public string? EmailMessage { get; set; }
    public ReportSchedulerFileFormat? EmailAttachmentFileFormat { get; set; }
    public ScheduledReportCategory? ReportCategory { get; set; }
    public ScheduledReportName? ReportName { get; set; }
    public ReportDateType? StartDateType { get; set; }
    public DateOnly? StartDate { get; set; }
    public ReportDateType? EndDateType { get; set; }
    public DateOnly? EndDate { get; set; }
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
    public bool Active { get; set; } = true;
    public ReportSchedulerStatus Status { get; set; } = ReportSchedulerStatus.Pending;
    public int? ApprovedById { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ReportSchedulerRunHistory> RunHistory { get; set; } = new List<ReportSchedulerRunHistory>();
}
