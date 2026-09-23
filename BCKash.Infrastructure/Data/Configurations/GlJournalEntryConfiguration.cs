using BCKash.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GlJournalEntryConfiguration : IEntityTypeConfiguration<GlJournalEntry>
{
    public void Configure(EntityTypeBuilder<GlJournalEntry> builder)
    {
        builder.ToTable("gl_journal_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OfficeId).HasColumnName("office_id");
        builder.Property(e => e.GlAccountId).HasColumnName("gl_account_id");
        builder.Property(e => e.CurrencyId).HasColumnName("currency_id");

        builder.Property(e => e.TransactionType)
            .HasColumnName("transaction_type")
            .HasConversion(
                t => t == null ? null
                    : t == GlTransactionType.Disbursement ? "disbursement"
                    : t == GlTransactionType.Accrual ? "accrual"
                    : t == GlTransactionType.Deposit ? "deposit"
                    : t == GlTransactionType.Withdrawal ? "withdrawal"
                    : t == GlTransactionType.ManualEntry ? "manual_entry"
                    : t == GlTransactionType.PayCharge ? "pay_charge"
                    : t == GlTransactionType.TransferFund ? "transfer_fund"
                    : t == GlTransactionType.Expense ? "expense"
                    : t == GlTransactionType.Payroll ? "payroll"
                    : t == GlTransactionType.Income ? "income"
                    : t == GlTransactionType.Fee ? "fee"
                    : t == GlTransactionType.Penalty ? "penalty"
                    : t == GlTransactionType.Interest ? "interest"
                    : t == GlTransactionType.Dividend ? "dividend"
                    : t == GlTransactionType.Guarantee ? "guarantee"
                    : t == GlTransactionType.WriteOff ? "write_off"
                    : t == GlTransactionType.Repayment ? "repayment"
                    : t == GlTransactionType.RepaymentDisbursement ? "repayment_disbursement"
                    : t == GlTransactionType.RepaymentRecovery ? "repayment_recovery"
                    : t == GlTransactionType.InterestAccrual ? "interest_accrual"
                    : t == GlTransactionType.FeeAccrual ? "fee_accrual"
                    : t == GlTransactionType.Savings ? "savings"
                    : t == GlTransactionType.Shares ? "shares"
                    : t == GlTransactionType.Asset ? "asset"
                    : t == GlTransactionType.AssetIncome ? "asset_income"
                    : t == GlTransactionType.AssetExpense ? "asset_expense"
                    : "asset_depreciation",
                s => s == null ? (GlTransactionType?)null
                    : s == "disbursement" ? GlTransactionType.Disbursement
                    : s == "accrual" ? GlTransactionType.Accrual
                    : s == "deposit" ? GlTransactionType.Deposit
                    : s == "withdrawal" ? GlTransactionType.Withdrawal
                    : s == "manual_entry" ? GlTransactionType.ManualEntry
                    : s == "pay_charge" ? GlTransactionType.PayCharge
                    : s == "transfer_fund" ? GlTransactionType.TransferFund
                    : s == "expense" ? GlTransactionType.Expense
                    : s == "payroll" ? GlTransactionType.Payroll
                    : s == "income" ? GlTransactionType.Income
                    : s == "fee" ? GlTransactionType.Fee
                    : s == "penalty" ? GlTransactionType.Penalty
                    : s == "interest" ? GlTransactionType.Interest
                    : s == "dividend" ? GlTransactionType.Dividend
                    : s == "guarantee" ? GlTransactionType.Guarantee
                    : s == "write_off" ? GlTransactionType.WriteOff
                    : s == "repayment" ? GlTransactionType.Repayment
                    : s == "repayment_disbursement" ? GlTransactionType.RepaymentDisbursement
                    : s == "repayment_recovery" ? GlTransactionType.RepaymentRecovery
                    : s == "interest_accrual" ? GlTransactionType.InterestAccrual
                    : s == "fee_accrual" ? GlTransactionType.FeeAccrual
                    : s == "savings" ? GlTransactionType.Savings
                    : s == "shares" ? GlTransactionType.Shares
                    : s == "asset" ? GlTransactionType.Asset
                    : s == "asset_income" ? GlTransactionType.AssetIncome
                    : s == "asset_expense" ? GlTransactionType.AssetExpense
                    : GlTransactionType.AssetDepreciation)
            .HasMaxLength(30);

        builder.Property(e => e.TransactionSubType)
            .HasColumnName("transaction_sub_type")
            .HasConversion(
                t => t == null ? null
                    : t == GlTransactionSubType.Overpayment ? "overpayment"
                    : t == GlTransactionSubType.RepaymentInterest ? "repayment_interest"
                    : t == GlTransactionSubType.RepaymentPrincipal ? "repayment_principal"
                    : t == GlTransactionSubType.RepaymentFees ? "repayment_fees"
                    : "repayment_penalty",
                s => s == null ? (GlTransactionSubType?)null
                    : s == "overpayment" ? GlTransactionSubType.Overpayment
                    : s == "repayment_interest" ? GlTransactionSubType.RepaymentInterest
                    : s == "repayment_principal" ? GlTransactionSubType.RepaymentPrincipal
                    : s == "repayment_fees" ? GlTransactionSubType.RepaymentFees
                    : GlTransactionSubType.RepaymentPenalty)
            .HasMaxLength(30);

        builder.Property(e => e.Debit).HasColumnName("debit").HasPrecision(65, 4);
        builder.Property(e => e.Credit).HasColumnName("credit").HasPrecision(65, 4);
        builder.Property(e => e.Reversed).HasColumnName("reversed").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name");
        builder.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(191);
        builder.Property(e => e.LoanId).HasColumnName("loan_id");
        builder.Property(e => e.LoanTransactionId).HasColumnName("loan_transaction_id");
        builder.Property(e => e.SavingsTransactionId).HasColumnName("savings_transaction_id");
        builder.Property(e => e.SavingsId).HasColumnName("savings_id");
        builder.Property(e => e.SharesTransactionId).HasColumnName("shares_transaction_id");
        builder.Property(e => e.PayrollTransactionId).HasColumnName("payroll_transaction_id");
        builder.Property(e => e.PaymentDetailId).HasColumnName("payment_detail_id");
        builder.Property(e => e.TransactionId).HasColumnName("transaction_id");
        builder.Property(e => e.GlClosureId).HasColumnName("gl_closure_id");
        builder.Property(e => e.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(e => e.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(e => e.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(e => e.Notes).HasColumnName("notes");
        builder.Property(e => e.Narration).HasColumnName("narration");
        builder.Property(e => e.CreatedById).HasColumnName("created_by_id");
        builder.Property(e => e.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(e => e.Reconciled).HasColumnName("reconciled").IsRequired();
        builder.Property(e => e.ManualEntry).HasColumnName("manual_entry").IsRequired();
        builder.Property(e => e.Approved).HasColumnName("approved").IsRequired();
        builder.Property(e => e.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(e => e.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(e => e.ApprovedNotes).HasColumnName("approved_notes");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.GlAccount)
            .WithMany(a => a.JournalEntries)
            .HasForeignKey(e => e.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.Savings)
            .WithMany()
            .HasForeignKey(e => e.SavingsId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.GlClosure)
            .WithMany(c => c.JournalEntries)
            .HasForeignKey(e => e.GlClosureId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.Reference);
        builder.HasIndex(e => new { e.OfficeId, e.Date });
    }
}
