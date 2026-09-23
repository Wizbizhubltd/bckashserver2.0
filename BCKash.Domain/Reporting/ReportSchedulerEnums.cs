namespace BCKash.Domain.Reporting;

/// <summary>Legacy values of `report_scheduler.recurrence_type`.</summary>
public enum ReportSchedulerRecurrenceType
{
    None,
    Schedule,
}

/// <summary>Legacy values of `report_scheduler.recur_frequency`.</summary>
public enum ReportSchedulerRecurFrequency
{
    Daily,
    Monthly,
    Weekly,
    Yearly,
}

/// <summary>Legacy values of `report_scheduler.email_attachment_file_format`.</summary>
public enum ReportSchedulerFileFormat
{
    Pdf,
    Csv,
    Xls,
}

/// <summary>Legacy values of `report_scheduler.report_category`.</summary>
public enum ScheduledReportCategory
{
    ClientReport,
    LoanReport,
    FinancialReport,
    GroupReport,
    SavingsReport,
    OrganisationReport,
}

/// <summary>Legacy values of `report_scheduler.report_name`.</summary>
public enum ScheduledReportName
{
    DisbursedLoansReport,
    LoanPortfolioReport,
    ExpectedRepaymentsReport,
    RepaymentsReport,
    CollectionReport,
    ArrearsReport,
    BalanceSheet,
    TrialBalance,
    ProfitAndLoss,
    CashFlow,
    Provisioning,
    HistoricalIncomeStatement,
    JournalsReport,
    AccruedInterest,
    ClientNumbersReport,
    ClientsOverview,
    TopClientsReport,
    LoanSizesReport,
    GroupReport,
    GroupBreakdown,
    SavingsAccountReport,
    SavingsBalanceReport,
    SavingsTransactionReport,
    FixedTermMaturityReport,
    ProductsSummary,
    IndividualIndicatorReport,
    LoanOfficerPerformanceReport,
    AuditReport,
    GroupIndicatorReport,
}

/// <summary>Legacy values of `report_scheduler.start_date_type` and `report_scheduler.end_date_type` (shared value set).</summary>
public enum ReportDateType
{
    DatePicker,
    Today,
    Yesterday,
    Tomorrow,
}

/// <summary>Legacy values of `report_scheduler.status`.</summary>
public enum ReportSchedulerStatus
{
    Pending,
    Approved,
    Declined,
}
