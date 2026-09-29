using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("loan_applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(a => a.ClientType)
            .HasColumnName("client_type")
            .HasConversion(
                v => v == LoanClientType.Client ? "client" : "group",
                v => v == "client" ? LoanClientType.Client : LoanClientType.Group)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.LoanId).HasColumnName("loan_id");
        builder.Property(a => a.LoanPurposeId).HasColumnName("loan_purpose_id");
        builder.Property(a => a.CurrencyId).HasColumnName("currency_id");
        builder.Property(a => a.OfficeId).HasColumnName("office_id");
        builder.Property(a => a.ClientId).HasColumnName("client_id");
        builder.Property(a => a.GroupId).HasColumnName("group_id");
        builder.Property(a => a.LoanProductId).HasColumnName("loan_product_id").IsRequired();

        builder.Property(a => a.Amount).HasColumnName("amount").HasPrecision(65, 4).IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == ApprovalStatus.Approved ? "approved" : v == ApprovalStatus.Pending ? "pending" : "declined",
                v => v == "approved" ? ApprovalStatus.Approved : v == "pending" ? ApprovalStatus.Pending : ApprovalStatus.Declined)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.GuarantorIds).HasColumnName("guarantor_ids");
        builder.Property(a => a.LoanTerm).HasColumnName("loan_term");

        builder.Property(a => a.LoanTermType)
            .HasColumnName("loan_term_type")
            .HasConversion(
                v => v == FrequencyType.Days ? "days" : v == FrequencyType.Weeks ? "weeks" : v == FrequencyType.Months ? "months" : "years",
                v => v == "days" ? FrequencyType.Days : v == "weeks" ? FrequencyType.Weeks : v == "months" ? FrequencyType.Months : FrequencyType.Years)
            .HasMaxLength(20);

        builder.Property(a => a.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(a => a.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(a => a.ApprovedNotes).HasColumnName("approved_notes");
        builder.Property(a => a.DeclinedNotes).HasColumnName("declined_notes");
        builder.Property(a => a.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(a => a.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(a => a.Notes).HasColumnName("notes");

        // New — how the loan is paid out, and for a bank transfer the account it goes to.
        builder.Property(x => x.DisbursementMode)
            .HasColumnName("disbursement_mode")
            .HasConversion(
                v => v == DisbursementMode.CashPickup ? "cash_pickup" : v == DisbursementMode.ChequePickup ? "cheque_pickup" : "bank_transfer",
                v => v == "cash_pickup" ? DisbursementMode.CashPickup : v == "cheque_pickup" ? DisbursementMode.ChequePickup : DisbursementMode.BankTransfer)
            .HasMaxLength(20);
        builder.Property(x => x.DisbursementBankName).HasColumnName("disbursement_bank_name").HasMaxLength(100);
        builder.Property(x => x.DisbursementAccountNumber).HasColumnName("disbursement_account_number").HasMaxLength(20);
        builder.Property(x => x.DisbursementAccountName).HasColumnName("disbursement_account_name").HasMaxLength(150);
        builder.Property(x => x.FormFee).HasColumnName("form_fee").HasPrecision(65, 4);
        builder.Property(x => x.FormFeeChargeId).HasColumnName("form_fee_charge_id");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.ClientId);
        builder.HasIndex(a => a.GroupId);
        builder.HasIndex(a => a.OfficeId);
        builder.HasIndex(a => a.LoanProductId);

        builder.HasOne(a => a.Loan)
            .WithMany(l => l.LoanApplications)
            .HasForeignKey(a => a.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(a => a.LoanProduct)
            .WithMany(p => p.LoanApplications)
            .HasForeignKey(a => a.LoanProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(a => a.LoanPurpose)
            .WithMany(p => p.LoanApplications)
            .HasForeignKey(a => a.LoanPurposeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
