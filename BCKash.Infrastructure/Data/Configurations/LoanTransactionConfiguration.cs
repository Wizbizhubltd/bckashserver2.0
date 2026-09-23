using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanTransactionConfiguration : IEntityTypeConfiguration<LoanTransaction>
{
    public void Configure(EntityTypeBuilder<LoanTransaction> builder)
    {
        builder.ToTable("loan_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.LoanId).HasColumnName("loan_id");
        builder.Property(t => t.OfficeId).HasColumnName("office_id");
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.PaymentTypeId).HasColumnName("payment_type_id");

        builder.Property(t => t.TransactionType)
            .HasColumnName("transaction_type")
            .HasConversion(
                v => v == LoanTransactionType.Repayment ? "repayment"
                    : v == LoanTransactionType.RepaymentDisbursement ? "repayment_disbursement"
                    : v == LoanTransactionType.WriteOff ? "write_off"
                    : v == LoanTransactionType.WriteOffRecovery ? "write_off_recovery"
                    : v == LoanTransactionType.Disbursement ? "disbursement"
                    : v == LoanTransactionType.InterestAccrual ? "interest_accrual"
                    : v == LoanTransactionType.FeeAccrual ? "fee_accrual"
                    : v == LoanTransactionType.PenaltyAccrual ? "penalty_accrual"
                    : v == LoanTransactionType.Deposit ? "deposit"
                    : v == LoanTransactionType.Withdrawal ? "withdrawal"
                    : v == LoanTransactionType.ManualEntry ? "manual_entry"
                    : v == LoanTransactionType.PayCharge ? "pay_charge"
                    : v == LoanTransactionType.TransferFund ? "transfer_fund"
                    : v == LoanTransactionType.Interest ? "interest"
                    : v == LoanTransactionType.Income ? "income"
                    : v == LoanTransactionType.Fee ? "fee"
                    : v == LoanTransactionType.DisbursementFee ? "disbursement_fee"
                    : v == LoanTransactionType.InstallmentFee ? "installment_fee"
                    : v == LoanTransactionType.SpecifiedDueDateFee ? "specified_due_date_fee"
                    : v == LoanTransactionType.OverdueMaturity ? "overdue_maturity"
                    : v == LoanTransactionType.OverdueInstallmentFee ? "overdue_installment_fee"
                    : v == LoanTransactionType.LoanReschedulingFee ? "loan_rescheduling_fee"
                    : v == LoanTransactionType.Penalty ? "penalty"
                    : v == LoanTransactionType.InterestWaiver ? "interest_waiver"
                    : "charge_waiver",
                v => v == "repayment" ? LoanTransactionType.Repayment
                    : v == "repayment_disbursement" ? LoanTransactionType.RepaymentDisbursement
                    : v == "write_off" ? LoanTransactionType.WriteOff
                    : v == "write_off_recovery" ? LoanTransactionType.WriteOffRecovery
                    : v == "disbursement" ? LoanTransactionType.Disbursement
                    : v == "interest_accrual" ? LoanTransactionType.InterestAccrual
                    : v == "fee_accrual" ? LoanTransactionType.FeeAccrual
                    : v == "penalty_accrual" ? LoanTransactionType.PenaltyAccrual
                    : v == "deposit" ? LoanTransactionType.Deposit
                    : v == "withdrawal" ? LoanTransactionType.Withdrawal
                    : v == "manual_entry" ? LoanTransactionType.ManualEntry
                    : v == "pay_charge" ? LoanTransactionType.PayCharge
                    : v == "transfer_fund" ? LoanTransactionType.TransferFund
                    : v == "interest" ? LoanTransactionType.Interest
                    : v == "income" ? LoanTransactionType.Income
                    : v == "fee" ? LoanTransactionType.Fee
                    : v == "disbursement_fee" ? LoanTransactionType.DisbursementFee
                    : v == "installment_fee" ? LoanTransactionType.InstallmentFee
                    : v == "specified_due_date_fee" ? LoanTransactionType.SpecifiedDueDateFee
                    : v == "overdue_maturity" ? LoanTransactionType.OverdueMaturity
                    : v == "overdue_installment_fee" ? LoanTransactionType.OverdueInstallmentFee
                    : v == "loan_rescheduling_fee" ? LoanTransactionType.LoanReschedulingFee
                    : v == "penalty" ? LoanTransactionType.Penalty
                    : v == "interest_waiver" ? LoanTransactionType.InterestWaiver
                    : LoanTransactionType.ChargeWaiver)
            .HasMaxLength(40);

        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(t => t.PaymentDetailId).HasColumnName("payment_detail_id");
        builder.Property(t => t.ChargeId).HasColumnName("charge_id");
        builder.Property(t => t.LoanRepaymentScheduleId).HasColumnName("loan_repayment_schedule_id");

        builder.Property(t => t.Debit).HasColumnName("debit").HasPrecision(65, 4);
        builder.Property(t => t.Credit).HasColumnName("credit").HasPrecision(65, 4);
        builder.Property(t => t.Balance).HasColumnName("balance").HasPrecision(65, 4);
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(65, 4);

        builder.Property(t => t.Reversible).HasColumnName("reversible").IsRequired();
        builder.Property(t => t.Reversed).HasColumnName("reversed").IsRequired();

        builder.Property(t => t.ReversalType)
            .HasColumnName("reversal_type")
            .HasConversion(
                v => v == LoanTransactionReversalType.System ? "system" : v == LoanTransactionReversalType.User ? "user" : "none",
                v => v == "system" ? LoanTransactionReversalType.System : v == "user" ? LoanTransactionReversalType.User : LoanTransactionReversalType.None)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.PaymentApplyTo)
            .HasColumnName("payment_apply_to")
            .HasConversion(
                v => v == LoanPaymentApplyTo.Interest ? "interest"
                    : v == LoanPaymentApplyTo.Principal ? "principal"
                    : v == LoanPaymentApplyTo.Fees ? "fees"
                    : v == LoanPaymentApplyTo.Penalty ? "penalty"
                    : "regular",
                v => v == "interest" ? LoanPaymentApplyTo.Interest
                    : v == "principal" ? LoanPaymentApplyTo.Principal
                    : v == "fees" ? LoanPaymentApplyTo.Fees
                    : v == "penalty" ? LoanPaymentApplyTo.Penalty
                    : LoanPaymentApplyTo.Regular)
            .HasMaxLength(20);

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == ApprovalStatus.Approved ? "approved" : v == ApprovalStatus.Pending ? "pending" : "declined",
                v => v == "approved" ? ApprovalStatus.Approved : v == "pending" ? ApprovalStatus.Pending : ApprovalStatus.Declined)
            .HasMaxLength(20);

        builder.Property(t => t.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(t => t.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");

        builder.Property(t => t.Interest).HasColumnName("interest").HasPrecision(65, 4);
        builder.Property(t => t.Principal).HasColumnName("principal").HasPrecision(65, 4);
        builder.Property(t => t.Fee).HasColumnName("fee").HasPrecision(65, 4);
        builder.Property(t => t.Penalty).HasColumnName("penalty").HasPrecision(65, 4);
        builder.Property(t => t.Overpayment).HasColumnName("overpayment").HasPrecision(65, 4);

        builder.Property(t => t.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(t => t.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(t => t.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(t => t.Receipt).HasColumnName("receipt");

        builder.Property(t => t.PrincipalDerived).HasColumnName("principal_derived").HasPrecision(65, 4);
        builder.Property(t => t.InterestDerived).HasColumnName("interest_derived").HasPrecision(65, 4);
        builder.Property(t => t.FeesDerived).HasColumnName("fees_derived").HasPrecision(65, 4);
        builder.Property(t => t.PenaltyDerived).HasColumnName("penalty_derived").HasPrecision(65, 4);
        builder.Property(t => t.OverpaymentDerived).HasColumnName("overpayment_derived").HasPrecision(65, 4);
        builder.Property(t => t.UnrecognizedIncomeDerived).HasColumnName("unrecognized_income_derived").HasPrecision(65, 4);

        builder.Property(t => t.Notes).HasColumnName("notes");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(t => t.PaymentDetailId).IsUnique();

        // Phase 0 scaffolding mistakenly made (LoanId, ClientId) a unique pair, which would
        // allow only one transaction ever per loan — broken on the second repayment. No BR-LN-5/
        // FR-LN-16..20 requirement implies that constraint; replaced with plain lookup indexes.
        builder.HasIndex(t => t.LoanId);
        builder.HasIndex(t => t.ClientId);
        builder.HasIndex(t => t.LoanRepaymentScheduleId);
        builder.HasIndex(t => t.Status);
        // Backs the dashboard's this-month disbursements/repayments count+sum queries.
        builder.HasIndex(t => new { t.TransactionType, t.Date });

        builder.HasOne(t => t.Loan)
            .WithMany(l => l.LoanTransactions)
            .HasForeignKey(t => t.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(t => t.LoanRepaymentSchedule)
            .WithMany(s => s.LoanTransactions)
            .HasForeignKey(t => t.LoanRepaymentScheduleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
