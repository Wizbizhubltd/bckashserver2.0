using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanTransactionRepaymentScheduleMappingConfiguration : IEntityTypeConfiguration<LoanTransactionRepaymentScheduleMapping>
{
    public void Configure(EntityTypeBuilder<LoanTransactionRepaymentScheduleMapping> builder)
    {
        builder.ToTable("loan_transaction_repayment_schedule_mappings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(m => m.LoanRepaymentScheduleId).HasColumnName("loan_repayment_schedule_id");
        builder.Property(m => m.LoanTransactionId).HasColumnName("loan_transaction_id");

        builder.Property(m => m.Interest).HasColumnName("interest").HasPrecision(65, 4);
        builder.Property(m => m.Principal).HasColumnName("principal").HasPrecision(65, 4);
        builder.Property(m => m.Fee).HasColumnName("fee").HasPrecision(65, 4);
        builder.Property(m => m.Penalty).HasColumnName("penalty").HasPrecision(65, 4);
        builder.Property(m => m.Overpayment).HasColumnName("overpayment").HasPrecision(65, 4);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(m => m.LoanRepaymentSchedule)
            .WithMany(s => s.LoanTransactionRepaymentScheduleMappings)
            .HasForeignKey(m => m.LoanRepaymentScheduleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(m => m.LoanTransaction)
            .WithMany(t => t.LoanTransactionRepaymentScheduleMappings)
            .HasForeignKey(m => m.LoanTransactionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
