namespace BCKash.Domain.Communications;

/// <summary>Legacy values of `communication_campaigns.type`.</summary>
public enum CampaignType
{
    Sms,
    Email,
}

/// <summary>Legacy values of `communication_campaigns.recurrence_type`.</summary>
public enum CampaignRecurrenceType
{
    None,
    Schedule,
}

/// <summary>Legacy values of `communication_campaigns.recur_frequency`.</summary>
public enum CampaignRecurFrequency
{
    Days,
    Months,
    Weeks,
    Years,
}

/// <summary>Legacy values of `communication_campaigns.email_attachment_file_format`.</summary>
public enum CampaignFileFormat
{
    Pdf,
    Csv,
    Xls,
}

/// <summary>Legacy values of `communication_campaigns.recipients_category`.</summary>
public enum CampaignRecipientsCategory
{
    AllClients,
    ActiveClients,
    ProspectiveClients,
    ActiveLoans,
    LoansInArrears,
    OverdueLoans,
    HappyBirthday,
}

/// <summary>Legacy values of `communication_campaigns.report_attachment`.</summary>
public enum CampaignReportAttachment
{
    LoanSchedule,
    LoanStatement,
    SavingsStatement,
    AuditReport,
    GroupIndicatorReport,
}

/// <summary>Legacy values of `communication_campaigns.status`.</summary>
public enum CampaignStatus
{
    Pending,
    Active,
    Declined,
    Inactive,
}
