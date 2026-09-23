using BCKash.Domain.Reporting;

namespace BCKash.Api.Contracts;

public record ReportCatalogEntryResponse(ScheduledReportName Name, ScheduledReportCategory Category);

public record ReportResultResponse(ScheduledReportName ReportName, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);

public record SaveReportScheduleRequest(
    string? Description,
    DateOnly? ReportStartDate,
    string? ReportStartTime,
    ReportSchedulerRecurrenceType? RecurrenceType,
    ReportSchedulerRecurFrequency? RecurFrequency,
    string? RecurInterval,
    string? EmailRecipients,
    string? EmailSubject,
    string? EmailMessage,
    ReportSchedulerFileFormat? EmailAttachmentFileFormat,
    ScheduledReportCategory? ReportCategory,
    ScheduledReportName? ReportName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? OfficeId,
    string? LoanOfficerId,
    string? LoanStatus,
    string? LoanProductId,
    bool Active);

public record ReportScheduleResponse(
    int Id,
    string? Description,
    ScheduledReportCategory? ReportCategory,
    ScheduledReportName? ReportName,
    ReportSchedulerRecurrenceType? RecurrenceType,
    ReportSchedulerFileFormat? EmailAttachmentFileFormat,
    DateOnly? LastRunDate,
    DateOnly? NextRunDate,
    int NumberOfRuns,
    bool Active,
    ReportSchedulerStatus Status);
