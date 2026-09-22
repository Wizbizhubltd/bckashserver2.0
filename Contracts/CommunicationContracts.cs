using BCKash.Domain.Communications;

namespace BCKash.Api.Contracts;

public record SaveCampaignRequest(
    CampaignType? Type,
    string? Name,
    string? Description,
    DateOnly? ReportStartDate,
    string? ReportStartTime,
    CampaignRecurrenceType? RecurrenceType,
    CampaignRecurFrequency? RecurFrequency,
    string? RecurInterval,
    string? EmailRecipients,
    string? EmailSubject,
    string? Message,
    CampaignFileFormat? EmailAttachmentFileFormat,
    CampaignRecipientsCategory? RecipientsCategory,
    CampaignReportAttachment? ReportAttachment,
    string? FromDay,
    string? ToDay,
    string? OfficeId,
    string? LoanOfficerId,
    string? LoanStatus,
    string? LoanProductId,
    bool Active);

public record CampaignResponse(
    int Id,
    CampaignType? Type,
    string? Name,
    string? Description,
    CampaignRecipientsCategory? RecipientsCategory,
    CampaignRecurrenceType? RecurrenceType,
    DateOnly? LastRunDate,
    DateOnly? NextRunDate,
    int NumberOfRuns,
    int NumberOfRecipients,
    bool Active,
    bool Sent,
    CampaignStatus Status);

public record CampaignRecipientResponse(int ClientId, string? Name, string? Mobile, string? Email);

public record SaveSmsGatewayRequest(string? Name, string? FromName, string? ToName, string? Url, string? MsgName, string? Notes);

public record SmsGatewayResponse(int Id, string? Name, string? FromName, string? ToName, string? Url, string? MsgName, string? Notes);

public record CreateReminderRequest(string Code);

public record ReminderResponse(int Id, int UserId, string Code, bool Completed, DateTime? CompletedAt);
