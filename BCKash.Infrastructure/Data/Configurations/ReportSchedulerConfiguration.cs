using BCKash.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ReportSchedulerConfiguration : IEntityTypeConfiguration<ReportScheduler>
{
    public void Configure(EntityTypeBuilder<ReportScheduler> builder)
    {
        builder.ToTable("report_scheduler");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.CreatedById).HasColumnName("created_by_id");
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.ReportStartDate).HasColumnName("report_start_date").HasColumnType("date");
        builder.Property(r => r.ReportStartTime).HasColumnName("report_start_time").HasMaxLength(191);

        builder.Property(r => r.RecurrenceType)
            .HasColumnName("recurrence_type")
            .HasConversion(
                t => t == null ? null : t == ReportSchedulerRecurrenceType.None ? "none" : "schedule",
                s => s == null ? (ReportSchedulerRecurrenceType?)null : s == "none" ? ReportSchedulerRecurrenceType.None : ReportSchedulerRecurrenceType.Schedule)
            .HasMaxLength(20);

        builder.Property(r => r.RecurFrequency)
            .HasColumnName("recur_frequency")
            .HasConversion(
                f => f == null ? null
                    : f == ReportSchedulerRecurFrequency.Daily ? "daily"
                    : f == ReportSchedulerRecurFrequency.Monthly ? "monthly"
                    : f == ReportSchedulerRecurFrequency.Weekly ? "weekly"
                    : "yearly",
                s => s == null ? (ReportSchedulerRecurFrequency?)null
                    : s == "daily" ? ReportSchedulerRecurFrequency.Daily
                    : s == "monthly" ? ReportSchedulerRecurFrequency.Monthly
                    : s == "weekly" ? ReportSchedulerRecurFrequency.Weekly
                    : ReportSchedulerRecurFrequency.Yearly)
            .HasMaxLength(20);

        builder.Property(r => r.RecurInterval).HasColumnName("recur_interval").HasMaxLength(191);
        builder.Property(r => r.EmailRecipients).HasColumnName("email_recipients");
        builder.Property(r => r.EmailSubject).HasColumnName("email_subject").HasMaxLength(191);
        builder.Property(r => r.EmailMessage).HasColumnName("email_message");

        builder.Property(r => r.EmailAttachmentFileFormat)
            .HasColumnName("email_attachment_file_format")
            .HasConversion(
                f => f == null ? null
                    : f == ReportSchedulerFileFormat.Pdf ? "pdf"
                    : f == ReportSchedulerFileFormat.Csv ? "csv"
                    : "xls",
                s => s == null ? (ReportSchedulerFileFormat?)null
                    : s == "pdf" ? ReportSchedulerFileFormat.Pdf
                    : s == "csv" ? ReportSchedulerFileFormat.Csv
                    : ReportSchedulerFileFormat.Xls)
            .HasMaxLength(10);

        builder.Property(r => r.ReportCategory)
            .HasColumnName("report_category")
            .HasConversion(
                c => c == null ? null
                    : c == ScheduledReportCategory.ClientReport ? "client_report"
                    : c == ScheduledReportCategory.LoanReport ? "loan_report"
                    : c == ScheduledReportCategory.FinancialReport ? "financial_report"
                    : c == ScheduledReportCategory.GroupReport ? "group_report"
                    : c == ScheduledReportCategory.SavingsReport ? "savings_report"
                    : "organisation_report",
                s => s == null ? (ScheduledReportCategory?)null
                    : s == "client_report" ? ScheduledReportCategory.ClientReport
                    : s == "loan_report" ? ScheduledReportCategory.LoanReport
                    : s == "financial_report" ? ScheduledReportCategory.FinancialReport
                    : s == "group_report" ? ScheduledReportCategory.GroupReport
                    : s == "savings_report" ? ScheduledReportCategory.SavingsReport
                    : ScheduledReportCategory.OrganisationReport)
            .HasMaxLength(30);

        builder.Property(r => r.ReportName)
            .HasColumnName("report_name")
            .HasConversion(
                n => n == null ? null
                    : n == ScheduledReportName.DisbursedLoansReport ? "disbursed_loans_report"
                    : n == ScheduledReportName.LoanPortfolioReport ? "loan_portfolio_report"
                    : n == ScheduledReportName.ExpectedRepaymentsReport ? "expected_repayments_report"
                    : n == ScheduledReportName.RepaymentsReport ? "repayments_report"
                    : n == ScheduledReportName.CollectionReport ? "collection_report"
                    : n == ScheduledReportName.ArrearsReport ? "arrears_report"
                    : n == ScheduledReportName.BalanceSheet ? "balance_sheet"
                    : n == ScheduledReportName.TrialBalance ? "trial_balance"
                    : n == ScheduledReportName.ProfitAndLoss ? "profit_and_loss"
                    : n == ScheduledReportName.CashFlow ? "cash_flow"
                    : n == ScheduledReportName.Provisioning ? "provisioning"
                    : n == ScheduledReportName.HistoricalIncomeStatement ? "historical_income_statement"
                    : n == ScheduledReportName.JournalsReport ? "journals_report"
                    : n == ScheduledReportName.AccruedInterest ? "accrued_interest"
                    : n == ScheduledReportName.ClientNumbersReport ? "client_numbers_report"
                    : n == ScheduledReportName.ClientsOverview ? "clients_overview"
                    : n == ScheduledReportName.TopClientsReport ? "top_clients_report"
                    : n == ScheduledReportName.LoanSizesReport ? "loan_sizes_report"
                    : n == ScheduledReportName.GroupReport ? "group_report"
                    : n == ScheduledReportName.GroupBreakdown ? "group_breakdown"
                    : n == ScheduledReportName.SavingsAccountReport ? "savings_account_report"
                    : n == ScheduledReportName.SavingsBalanceReport ? "savings_balance_report"
                    : n == ScheduledReportName.SavingsTransactionReport ? "savings_transaction_report"
                    : n == ScheduledReportName.FixedTermMaturityReport ? "fixed_term_maturity_report"
                    : n == ScheduledReportName.ProductsSummary ? "products_summary"
                    : n == ScheduledReportName.IndividualIndicatorReport ? "individual_indicator_report"
                    : n == ScheduledReportName.LoanOfficerPerformanceReport ? "loan_officer_performance_report"
                    : n == ScheduledReportName.AuditReport ? "audit_report"
                    : "group_indicator_report",
                s => s == null ? (ScheduledReportName?)null
                    : s == "disbursed_loans_report" ? ScheduledReportName.DisbursedLoansReport
                    : s == "loan_portfolio_report" ? ScheduledReportName.LoanPortfolioReport
                    : s == "expected_repayments_report" ? ScheduledReportName.ExpectedRepaymentsReport
                    : s == "repayments_report" ? ScheduledReportName.RepaymentsReport
                    : s == "collection_report" ? ScheduledReportName.CollectionReport
                    : s == "arrears_report" ? ScheduledReportName.ArrearsReport
                    : s == "balance_sheet" ? ScheduledReportName.BalanceSheet
                    : s == "trial_balance" ? ScheduledReportName.TrialBalance
                    : s == "profit_and_loss" ? ScheduledReportName.ProfitAndLoss
                    : s == "cash_flow" ? ScheduledReportName.CashFlow
                    : s == "provisioning" ? ScheduledReportName.Provisioning
                    : s == "historical_income_statement" ? ScheduledReportName.HistoricalIncomeStatement
                    : s == "journals_report" ? ScheduledReportName.JournalsReport
                    : s == "accrued_interest" ? ScheduledReportName.AccruedInterest
                    : s == "client_numbers_report" ? ScheduledReportName.ClientNumbersReport
                    : s == "clients_overview" ? ScheduledReportName.ClientsOverview
                    : s == "top_clients_report" ? ScheduledReportName.TopClientsReport
                    : s == "loan_sizes_report" ? ScheduledReportName.LoanSizesReport
                    : s == "group_report" ? ScheduledReportName.GroupReport
                    : s == "group_breakdown" ? ScheduledReportName.GroupBreakdown
                    : s == "savings_account_report" ? ScheduledReportName.SavingsAccountReport
                    : s == "savings_balance_report" ? ScheduledReportName.SavingsBalanceReport
                    : s == "savings_transaction_report" ? ScheduledReportName.SavingsTransactionReport
                    : s == "fixed_term_maturity_report" ? ScheduledReportName.FixedTermMaturityReport
                    : s == "products_summary" ? ScheduledReportName.ProductsSummary
                    : s == "individual_indicator_report" ? ScheduledReportName.IndividualIndicatorReport
                    : s == "loan_officer_performance_report" ? ScheduledReportName.LoanOfficerPerformanceReport
                    : s == "audit_report" ? ScheduledReportName.AuditReport
                    : ScheduledReportName.GroupIndicatorReport)
            .HasMaxLength(40);

        builder.Property(r => r.StartDateType)
            .HasColumnName("start_date_type")
            .HasConversion(
                d => d == null ? null
                    : d == ReportDateType.DatePicker ? "date_picker"
                    : d == ReportDateType.Today ? "today"
                    : d == ReportDateType.Yesterday ? "yesterday"
                    : "tomorrow",
                s => s == null ? (ReportDateType?)null
                    : s == "date_picker" ? ReportDateType.DatePicker
                    : s == "today" ? ReportDateType.Today
                    : s == "yesterday" ? ReportDateType.Yesterday
                    : ReportDateType.Tomorrow)
            .HasMaxLength(20);

        builder.Property(r => r.StartDate).HasColumnName("start_date").HasColumnType("date");

        builder.Property(r => r.EndDateType)
            .HasColumnName("end_date_type")
            .HasConversion(
                d => d == null ? null
                    : d == ReportDateType.DatePicker ? "date_picker"
                    : d == ReportDateType.Today ? "today"
                    : d == ReportDateType.Yesterday ? "yesterday"
                    : "tomorrow",
                s => s == null ? (ReportDateType?)null
                    : s == "date_picker" ? ReportDateType.DatePicker
                    : s == "today" ? ReportDateType.Today
                    : s == "yesterday" ? ReportDateType.Yesterday
                    : ReportDateType.Tomorrow)
            .HasMaxLength(20);

        builder.Property(r => r.EndDate).HasColumnName("end_date").HasColumnType("date");

        builder.Property(r => r.OfficeId).HasColumnName("office_id").HasMaxLength(191);
        builder.Property(r => r.LoanOfficerId).HasColumnName("loan_officer_id").HasMaxLength(191);
        builder.Property(r => r.GlAccountId).HasColumnName("gl_account_id").HasMaxLength(191);
        builder.Property(r => r.ManualEntries).HasColumnName("manual_entries").HasMaxLength(191);
        builder.Property(r => r.LoanStatus).HasColumnName("loan_status").HasMaxLength(191);
        builder.Property(r => r.LoanProductId).HasColumnName("loan_product_id").HasMaxLength(191);
        builder.Property(r => r.LastRunDate).HasColumnName("last_run_date").HasColumnType("date");
        builder.Property(r => r.NextRunDate).HasColumnName("next_run_date").HasColumnType("date");
        builder.Property(r => r.LastRunTime).HasColumnName("last_run_time").HasColumnType("date");
        builder.Property(r => r.NextRunTime).HasColumnName("next_run_time").HasColumnType("date");
        builder.Property(r => r.NumberOfRuns).HasColumnName("number_of_runs").IsRequired();
        builder.Property(r => r.Active).HasColumnName("active").IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(
                st => st == ReportSchedulerStatus.Pending ? "pending" : st == ReportSchedulerStatus.Approved ? "approved" : "declined",
                s => s == "pending" ? ReportSchedulerStatus.Pending : s == "approved" ? ReportSchedulerStatus.Approved : ReportSchedulerStatus.Declined)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}
