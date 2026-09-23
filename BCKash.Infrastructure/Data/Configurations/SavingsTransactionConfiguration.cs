using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SavingsTransactionConfiguration : IEntityTypeConfiguration<SavingsTransaction>
{
    public void Configure(EntityTypeBuilder<SavingsTransaction> builder)
    {
        builder.ToTable("savings_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.OfficeId).HasColumnName("office_id");
        builder.Property(t => t.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(t => t.PaymentDetailId).HasColumnName("payment_detail_id");
        builder.Property(t => t.SavingsId).HasColumnName("savings_id");
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(10, 2);
        builder.Property(t => t.Debit).HasColumnName("debit").HasPrecision(65, 4);
        builder.Property(t => t.Credit).HasColumnName("credit").HasPrecision(65, 4);
        builder.Property(t => t.Balance).HasColumnName("balance").HasPrecision(65, 4);

        builder.Property(t => t.TransactionType)
            .HasColumnName("transaction_type")
            .HasConversion(
                tt => tt == null ? null
                    : tt == SavingsTransactionType.Deposit ? "deposit"
                    : tt == SavingsTransactionType.Withdrawal ? "withdrawal"
                    : tt == SavingsTransactionType.BankFees ? "bank_fees"
                    : tt == SavingsTransactionType.Interest ? "interest"
                    : tt == SavingsTransactionType.Dividend ? "dividend"
                    : tt == SavingsTransactionType.Guarantee ? "guarantee"
                    : tt == SavingsTransactionType.GuaranteeRestored ? "guarantee_restored"
                    : tt == SavingsTransactionType.FeesPayment ? "fees_payment"
                    : tt == SavingsTransactionType.TransferLoan ? "transfer_loan"
                    : tt == SavingsTransactionType.TransferSavings ? "transfer_savings"
                    : "specified_due_date_fee",
                s => s == null ? (SavingsTransactionType?)null
                    : s == "deposit" ? SavingsTransactionType.Deposit
                    : s == "withdrawal" ? SavingsTransactionType.Withdrawal
                    : s == "bank_fees" ? SavingsTransactionType.BankFees
                    : s == "interest" ? SavingsTransactionType.Interest
                    : s == "dividend" ? SavingsTransactionType.Dividend
                    : s == "guarantee" ? SavingsTransactionType.Guarantee
                    : s == "guarantee_restored" ? SavingsTransactionType.GuaranteeRestored
                    : s == "fees_payment" ? SavingsTransactionType.FeesPayment
                    : s == "transfer_loan" ? SavingsTransactionType.TransferLoan
                    : s == "transfer_savings" ? SavingsTransactionType.TransferSavings
                    : SavingsTransactionType.SpecifiedDueDateFee)
            .HasMaxLength(30);

        builder.Property(t => t.Reversible).HasColumnName("reversible").IsRequired();
        builder.Property(t => t.Reversed).HasColumnName("reversed").IsRequired();

        builder.Property(t => t.ReversalType)
            .HasColumnName("reversal_type")
            .HasConversion(
                r => r == SavingsTransactionReversalType.System ? "system" : r == SavingsTransactionReversalType.User ? "user" : "none",
                s => s == "system" ? SavingsTransactionReversalType.System : s == "user" ? SavingsTransactionReversalType.User : SavingsTransactionReversalType.None)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(
                st => st == null ? null : st == SavingsTransactionStatus.Pending ? "pending" : st == SavingsTransactionStatus.Approved ? "approved" : "declined",
                s => s == null ? (SavingsTransactionStatus?)null : s == "pending" ? SavingsTransactionStatus.Pending : s == "approved" ? SavingsTransactionStatus.Approved : SavingsTransactionStatus.Declined)
            .HasMaxLength(20);

        builder.Property(t => t.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(t => t.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(t => t.SystemInterest).HasColumnName("system_interest").IsRequired();
        builder.Property(t => t.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(t => t.Time).HasColumnName("time").HasMaxLength(191);
        builder.Property(t => t.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(t => t.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.BalanceDate).HasColumnName("balance_date").HasColumnType("date");
        builder.Property(t => t.BalanceDays).HasColumnName("balance_days");
        builder.Property(t => t.CumulativeBalanceDays).HasColumnName("cumulative_balance_days");
        builder.Property(t => t.CumulativeBalance).HasColumnName("cumulative_balance").HasPrecision(65, 4);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        // Phase-0-scaffolding bug fix (same class as Phase 5's LoanTransactionConfiguration): a
        // payment detail isn't a one-transaction-ever key, so this must not be unique.
        builder.HasIndex(t => t.PaymentDetailId);
        builder.HasIndex(t => t.Balance);
        builder.HasIndex(t => t.TransactionType);
        builder.HasIndex(t => new { t.SavingsId, t.Amount });
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.SavingsId);

        builder.HasOne(t => t.Savings)
            .WithMany()
            .HasForeignKey(t => t.SavingsId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
