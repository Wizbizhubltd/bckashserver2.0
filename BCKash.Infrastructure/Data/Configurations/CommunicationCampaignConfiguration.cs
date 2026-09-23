using BCKash.Domain.Communications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class CommunicationCampaignConfiguration : IEntityTypeConfiguration<CommunicationCampaign>
{
    public void Configure(EntityTypeBuilder<CommunicationCampaign> builder)
    {
        builder.ToTable("communication_campaigns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");

        builder.Property(c => c.Type)
            .HasColumnName("type")
            .HasConversion(
                t => t == null ? null : t == CampaignType.Sms ? "sms" : "email",
                s => s == null ? (CampaignType?)null : s == "sms" ? CampaignType.Sms : CampaignType.Email)
            .HasMaxLength(20);

        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.ReportStartDate).HasColumnName("report_start_date").HasColumnType("date");
        builder.Property(c => c.ReportStartTime).HasColumnName("report_start_time").HasMaxLength(191);

        builder.Property(c => c.RecurrenceType)
            .HasColumnName("recurrence_type")
            .HasConversion(
                r => r == null ? null : r == CampaignRecurrenceType.None ? "none" : "schedule",
                s => s == null ? (CampaignRecurrenceType?)null : s == "none" ? CampaignRecurrenceType.None : CampaignRecurrenceType.Schedule)
            .HasMaxLength(20);

        builder.Property(c => c.RecurFrequency)
            .HasColumnName("recur_frequency")
            .HasConversion(
                f => f == null ? null
                    : f == CampaignRecurFrequency.Days ? "days"
                    : f == CampaignRecurFrequency.Months ? "months"
                    : f == CampaignRecurFrequency.Weeks ? "weeks"
                    : "years",
                s => s == null ? (CampaignRecurFrequency?)null
                    : s == "days" ? CampaignRecurFrequency.Days
                    : s == "months" ? CampaignRecurFrequency.Months
                    : s == "weeks" ? CampaignRecurFrequency.Weeks
                    : CampaignRecurFrequency.Years)
            .HasMaxLength(20);

        builder.Property(c => c.RecurInterval).HasColumnName("recur_interval").HasMaxLength(191);
        builder.Property(c => c.EmailRecipients).HasColumnName("email_recipients");
        builder.Property(c => c.EmailSubject).HasColumnName("email_subject").HasMaxLength(191);
        builder.Property(c => c.Message).HasColumnName("message");

        builder.Property(c => c.EmailAttachmentFileFormat)
            .HasColumnName("email_attachment_file_format")
            .HasConversion(
                f => f == null ? null
                    : f == CampaignFileFormat.Pdf ? "pdf"
                    : f == CampaignFileFormat.Csv ? "csv"
                    : "xls",
                s => s == null ? (CampaignFileFormat?)null
                    : s == "pdf" ? CampaignFileFormat.Pdf
                    : s == "csv" ? CampaignFileFormat.Csv
                    : CampaignFileFormat.Xls)
            .HasMaxLength(10);

        builder.Property(c => c.RecipientsCategory)
            .HasColumnName("recipients_category")
            .HasConversion(
                r => r == null ? null
                    : r == CampaignRecipientsCategory.AllClients ? "all_clients"
                    : r == CampaignRecipientsCategory.ActiveClients ? "active_clients"
                    : r == CampaignRecipientsCategory.ProspectiveClients ? "prospective_clients"
                    : r == CampaignRecipientsCategory.ActiveLoans ? "active_loans"
                    : r == CampaignRecipientsCategory.LoansInArrears ? "loans_in_arrears"
                    : r == CampaignRecipientsCategory.OverdueLoans ? "overdue_loans"
                    : "happy_birthday",
                s => s == null ? (CampaignRecipientsCategory?)null
                    : s == "all_clients" ? CampaignRecipientsCategory.AllClients
                    : s == "active_clients" ? CampaignRecipientsCategory.ActiveClients
                    : s == "prospective_clients" ? CampaignRecipientsCategory.ProspectiveClients
                    : s == "active_loans" ? CampaignRecipientsCategory.ActiveLoans
                    : s == "loans_in_arrears" ? CampaignRecipientsCategory.LoansInArrears
                    : s == "overdue_loans" ? CampaignRecipientsCategory.OverdueLoans
                    : CampaignRecipientsCategory.HappyBirthday)
            .HasMaxLength(30);

        builder.Property(c => c.ReportAttachment)
            .HasColumnName("report_attachment")
            .HasConversion(
                r => r == null ? null
                    : r == CampaignReportAttachment.LoanSchedule ? "loan_schedule"
                    : r == CampaignReportAttachment.LoanStatement ? "loan_statement"
                    : r == CampaignReportAttachment.SavingsStatement ? "savings_statement"
                    : r == CampaignReportAttachment.AuditReport ? "audit_report"
                    : "group_indicator_report",
                s => s == null ? (CampaignReportAttachment?)null
                    : s == "loan_schedule" ? CampaignReportAttachment.LoanSchedule
                    : s == "loan_statement" ? CampaignReportAttachment.LoanStatement
                    : s == "savings_statement" ? CampaignReportAttachment.SavingsStatement
                    : s == "audit_report" ? CampaignReportAttachment.AuditReport
                    : CampaignReportAttachment.GroupIndicatorReport)
            .HasMaxLength(30);

        builder.Property(c => c.FromDay).HasColumnName("from_day").HasMaxLength(191);
        builder.Property(c => c.ToDay).HasColumnName("to_day").HasMaxLength(191);
        builder.Property(c => c.OfficeId).HasColumnName("office_id").HasMaxLength(191);
        builder.Property(c => c.LoanOfficerId).HasColumnName("loan_officer_id").HasMaxLength(191);
        builder.Property(c => c.GlAccountId).HasColumnName("gl_account_id").HasMaxLength(191);
        builder.Property(c => c.ManualEntries).HasColumnName("manual_entries").HasMaxLength(191);
        builder.Property(c => c.LoanStatus).HasColumnName("loan_status").HasMaxLength(191);
        builder.Property(c => c.LoanProductId).HasColumnName("loan_product_id").HasMaxLength(191);
        builder.Property(c => c.LastRunDate).HasColumnName("last_run_date").HasColumnType("date");
        builder.Property(c => c.NextRunDate).HasColumnName("next_run_date").HasColumnType("date");
        builder.Property(c => c.LastRunTime).HasColumnName("last_run_time").HasColumnType("date");
        builder.Property(c => c.NextRunTime).HasColumnName("next_run_time").HasColumnType("date");
        builder.Property(c => c.NumberOfRuns).HasColumnName("number_of_runs").IsRequired();
        builder.Property(c => c.NumberOfRecipients).HasColumnName("number_of_recipients").IsRequired();
        builder.Property(c => c.Active).HasColumnName("active").IsRequired();
        builder.Property(c => c.Sent).HasColumnName("sent").IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(
                st => st == CampaignStatus.Pending ? "pending"
                    : st == CampaignStatus.Active ? "active"
                    : st == CampaignStatus.Declined ? "declined"
                    : "inactive",
                s => s == "pending" ? CampaignStatus.Pending
                    : s == "active" ? CampaignStatus.Active
                    : s == "declined" ? CampaignStatus.Declined
                    : CampaignStatus.Inactive)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}
