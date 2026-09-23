using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanRepaymentScheduleConfiguration : IEntityTypeConfiguration<LoanRepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<LoanRepaymentSchedule> builder)
    {
        builder.ToTable("loan_repayment_schedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(s => s.LoanId).HasColumnName("loan_id");
        builder.Property(s => s.Installment).HasColumnName("installment");
        builder.Property(s => s.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(s => s.FromDate).HasColumnName("from_date").HasColumnType("date");
        builder.Property(s => s.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(s => s.Year).HasColumnName("year").HasMaxLength(191);

        builder.Property(s => s.Principal).HasColumnName("principal").HasPrecision(65, 4);
        builder.Property(s => s.PrincipalWaived).HasColumnName("principal_waived").HasPrecision(65, 4);
        builder.Property(s => s.PrincipalWrittenOff).HasColumnName("principal_written_off").HasPrecision(65, 4);
        builder.Property(s => s.PrincipalPaid).HasColumnName("principal_paid").HasPrecision(65, 4);

        builder.Property(s => s.Interest).HasColumnName("interest").HasPrecision(65, 4);
        builder.Property(s => s.InterestWaived).HasColumnName("interest_waived").HasPrecision(65, 4);
        builder.Property(s => s.InterestWrittenOff).HasColumnName("interest_written_off").HasPrecision(65, 4);
        builder.Property(s => s.InterestPaid).HasColumnName("interest_paid").HasPrecision(65, 4);

        builder.Property(s => s.Fees).HasColumnName("fees").HasPrecision(65, 4);
        builder.Property(s => s.FeesWaived).HasColumnName("fees_waived").HasPrecision(65, 4);
        builder.Property(s => s.FeesWrittenOff).HasColumnName("fees_written_off").HasPrecision(65, 4);
        builder.Property(s => s.FeesPaid).HasColumnName("fees_paid").HasPrecision(65, 4);

        builder.Property(s => s.Penalty).HasColumnName("penalty").HasPrecision(65, 4);
        builder.Property(s => s.PenaltyWaived).HasColumnName("penalty_waived").HasPrecision(65, 4);
        builder.Property(s => s.PenaltyWrittenOff).HasColumnName("penalty_written_off").HasPrecision(65, 4);
        builder.Property(s => s.PenaltyPaid).HasColumnName("penalty_paid").HasPrecision(65, 4);

        builder.Property(s => s.TotalDue).HasColumnName("total_due").HasPrecision(65, 4);
        builder.Property(s => s.TotalPaidAdvance).HasColumnName("total_paid_advance").HasPrecision(65, 4);
        builder.Property(s => s.TotalPaidLate).HasColumnName("total_paid_late").HasPrecision(65, 4);

        builder.Property(s => s.Paid).HasColumnName("paid").IsRequired();
        builder.Property(s => s.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.LoanId);
        // Backs the dashboard's "late loans" query (installments past due, unpaid amount still
        // outstanding) — without it, that scan is a full table scan on a table that reaches into
        // the millions of rows. Indexed on DueDate alone, not a (Paid, DueDate) composite: on
        // real imported data `Paid` was found to be unset (0) on every single row regardless of
        // actual payment status, so the dashboard query filters by paid amount vs. Principal
        // instead — a composite index can't be used once its leftmost column isn't constrained.
        builder.HasIndex(s => s.DueDate);

        builder.HasOne(s => s.Loan)
            .WithMany(l => l.LoanRepaymentSchedules)
            .HasForeignKey(s => s.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
