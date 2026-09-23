using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(l => l.ClientType)
            .HasColumnName("client_type")
            .HasConversion(
                v => v == LoanClientType.Client ? "client" : "group",
                v => v == "client" ? LoanClientType.Client : LoanClientType.Group)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.LoanProductId).HasColumnName("loan_product_id");
        builder.Property(l => l.ClientId).HasColumnName("client_id");
        builder.Property(l => l.OldClientId).HasColumnName("old_client_id");
        builder.Property(l => l.OfficeId).HasColumnName("office_id");
        builder.Property(l => l.GroupId).HasColumnName("group_id");
        builder.Property(l => l.FundId).HasColumnName("fund_id");
        builder.Property(l => l.LoanPurposeId).HasColumnName("loan_purpose_id");
        builder.Property(l => l.CurrencyId).HasColumnName("currency_id");
        builder.Property(l => l.Decimals).HasColumnName("decimals").IsRequired();
        builder.Property(l => l.AccountNumber).HasColumnName("account_number").HasMaxLength(191);
        builder.Property(l => l.ExternalId).HasColumnName("external_id").HasMaxLength(191);
        builder.Property(l => l.LoanOfficerId).HasColumnName("loan_officer_id");

        builder.Property(l => l.Principal).HasColumnName("principal").HasPrecision(65, 4);
        builder.Property(l => l.AppliedAmount).HasColumnName("applied_amount").HasPrecision(65, 4);
        builder.Property(l => l.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(65, 4);
        builder.Property(l => l.PrincipalDerived).HasColumnName("principal_derived").HasPrecision(65, 4);
        builder.Property(l => l.InterestDerived).HasColumnName("interest_derived").HasPrecision(65, 4);
        builder.Property(l => l.FeesDerived).HasColumnName("fees_derived").HasPrecision(65, 4);
        builder.Property(l => l.PenaltyDerived).HasColumnName("penalty_derived").HasPrecision(65, 4);
        builder.Property(l => l.DisbursementFees).HasColumnName("disbursement_fees").HasPrecision(65, 4);
        builder.Property(l => l.ProcessingFee).HasColumnName("processing_fee").HasPrecision(65, 4);

        builder.Property(l => l.LoanTerm).HasColumnName("loan_term");
        builder.Property(l => l.LoanTermType)
            .HasColumnName("loan_term_type")
            .HasConversion(
                v => v == FrequencyType.Days ? "days" : v == FrequencyType.Weeks ? "weeks" : v == FrequencyType.Months ? "months" : "years",
                v => v == "days" ? FrequencyType.Days : v == "weeks" ? FrequencyType.Weeks : v == "months" ? FrequencyType.Months : FrequencyType.Years)
            .HasMaxLength(20);

        builder.Property(l => l.RepaymentFrequency).HasColumnName("repayment_frequency");
        builder.Property(l => l.RepaymentFrequencyType)
            .HasColumnName("repayment_frequency_type")
            .HasConversion(
                v => v == FrequencyType.Days ? "days" : v == FrequencyType.Weeks ? "weeks" : v == FrequencyType.Months ? "months" : "years",
                v => v == "days" ? FrequencyType.Days : v == "weeks" ? FrequencyType.Weeks : v == "months" ? FrequencyType.Months : FrequencyType.Years)
            .HasMaxLength(20);

        builder.Property(l => l.OverrideInterest).HasColumnName("override_interest").IsRequired();
        builder.Property(l => l.InterestRate).HasColumnName("interest_rate").HasPrecision(65, 4);
        builder.Property(l => l.OverrideInterestRate).HasColumnName("override_interest_rate").HasPrecision(65, 4);

        builder.Property(l => l.InterestRateType)
            .HasColumnName("interest_rate_type")
            .HasConversion(
                v => v == InterestRateFrequencyType.Day ? "day" : v == InterestRateFrequencyType.Week ? "week" : v == InterestRateFrequencyType.Month ? "month" : "year",
                v => v == "day" ? InterestRateFrequencyType.Day : v == "week" ? InterestRateFrequencyType.Week : v == "month" ? InterestRateFrequencyType.Month : InterestRateFrequencyType.Year)
            .HasMaxLength(20);

        builder.Property(l => l.ExpectedDisbursementDate).HasColumnName("expected_disbursement_date").HasColumnType("date");
        builder.Property(l => l.DisbursementDate).HasColumnName("disbursement_date").HasColumnType("date");
        builder.Property(l => l.ExpectedMaturityDate).HasColumnName("expected_maturity_date").HasColumnType("date");
        builder.Property(l => l.ExpectedFirstRepaymentDate).HasColumnName("expected_first_repayment_date").HasColumnType("date");
        builder.Property(l => l.RepaymentsNumber).HasColumnName("repayments_number");
        builder.Property(l => l.FirstRepaymentDate).HasColumnName("first_repayment_date").HasColumnType("date");

        builder.Property(l => l.InterestMethod)
            .HasColumnName("interest_method")
            .HasConversion(
                v => v == LoanInterestMethod.Flat ? "flat" : "declining_balance",
                v => v == "flat" ? LoanInterestMethod.Flat : LoanInterestMethod.DecliningBalance)
            .HasMaxLength(30);

        builder.Property(l => l.AmortizationMethod)
            .HasColumnName("armotization_method")
            .HasConversion(
                v => v == LoanAmortizationMethod.EqualInstallment ? "equal_installment" : "equal_principal",
                v => v == "equal_installment" ? LoanAmortizationMethod.EqualInstallment : LoanAmortizationMethod.EqualPrincipal)
            .HasMaxLength(30);

        builder.Property(l => l.GraceOnInterestCharged).HasColumnName("grace_on_interest_charged");
        builder.Property(l => l.GraceOnPrincipal).HasColumnName("grace_on_principal");
        builder.Property(l => l.GraceOnInterestPayment).HasColumnName("grace_on_interest_payment");

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == LoanStatus.New ? "new"
                    : v == LoanStatus.Pending ? "pending"
                    : v == LoanStatus.Approved ? "approved"
                    : v == LoanStatus.NeedChanges ? "need_changes"
                    : v == LoanStatus.Disbursed ? "disbursed"
                    : v == LoanStatus.Declined ? "declined"
                    : v == LoanStatus.Rejected ? "rejected"
                    : v == LoanStatus.Withdrawn ? "withdrawn"
                    : v == LoanStatus.WrittenOff ? "written_off"
                    : v == LoanStatus.Closed ? "closed"
                    : v == LoanStatus.PendingReschedule ? "pending_reschedule"
                    : v == LoanStatus.Rescheduled ? "rescheduled"
                    : "paid",
                v => v == "new" ? LoanStatus.New
                    : v == "pending" ? LoanStatus.Pending
                    : v == "approved" ? LoanStatus.Approved
                    : v == "need_changes" ? LoanStatus.NeedChanges
                    : v == "disbursed" ? LoanStatus.Disbursed
                    : v == "declined" ? LoanStatus.Declined
                    : v == "rejected" ? LoanStatus.Rejected
                    : v == "withdrawn" ? LoanStatus.Withdrawn
                    : v == "written_off" ? LoanStatus.WrittenOff
                    : v == "closed" ? LoanStatus.Closed
                    : v == "pending_reschedule" ? LoanStatus.PendingReschedule
                    : v == "rescheduled" ? LoanStatus.Rescheduled
                    : LoanStatus.Paid)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.CreatedById).HasColumnName("created_by_id");
        builder.Property(l => l.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(l => l.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(l => l.NeedChangesById).HasColumnName("need_changes_by_id");
        builder.Property(l => l.WithdrawnById).HasColumnName("withdrawn_by_id");
        builder.Property(l => l.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(l => l.WrittenOffById).HasColumnName("written_off_by_id");
        builder.Property(l => l.DisbursedById).HasColumnName("disbursed_by_id");
        builder.Property(l => l.RescheduledById).HasColumnName("rescheduled_by_id");
        builder.Property(l => l.ClosedById).HasColumnName("closed_by_id");

        builder.Property(l => l.CreatedDate).HasColumnName("created_date").HasColumnType("date");
        builder.Property(l => l.ModifiedDate).HasColumnName("modified_date").HasColumnType("date");
        builder.Property(l => l.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(l => l.NeedChangesDate).HasColumnName("need_changes_date").HasColumnType("date");
        builder.Property(l => l.WithdrawnDate).HasColumnName("withdrawn_date").HasColumnType("date");
        builder.Property(l => l.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(l => l.WrittenOffDate).HasColumnName("written_off_date").HasColumnType("date");
        builder.Property(l => l.RescheduledDate).HasColumnName("rescheduled_date").HasColumnType("date");
        builder.Property(l => l.ClosedDate).HasColumnName("closed_date").HasColumnType("date");

        builder.Property(l => l.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(l => l.Year).HasColumnName("year").HasMaxLength(191);

        builder.Property(l => l.Notes).HasColumnName("notes");
        builder.Property(l => l.ApprovedNotes).HasColumnName("approved_notes");
        builder.Property(l => l.DeclinedNotes).HasColumnName("declined_notes");
        builder.Property(l => l.WrittenOffNotes).HasColumnName("written_off_notes");
        builder.Property(l => l.DisbursedNotes).HasColumnName("disbursed_notes");
        builder.Property(l => l.WithdrawnNotes).HasColumnName("withdrawn_notes");
        builder.Property(l => l.RescheduledNotes).HasColumnName("rescheduled_notes");
        builder.Property(l => l.ClosedNotes).HasColumnName("closed_notes");

        // New in Phase 5 — the legacy schema has no such columns; see Loan.cs.
        builder.Property(l => l.IsNpa).HasColumnName("is_npa").IsRequired();
        builder.Property(l => l.IncomeSuspended).HasColumnName("income_suspended").IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(l => l.AccountNumber).IsUnique();
        builder.HasIndex(l => l.ClientId);
        builder.HasIndex(l => l.Status);

        builder.HasOne(l => l.LoanProduct)
            .WithMany(p => p.Loans)
            .HasForeignKey(l => l.LoanProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(l => l.LoanPurpose)
            .WithMany(p => p.Loans)
            .HasForeignKey(l => l.LoanPurposeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
