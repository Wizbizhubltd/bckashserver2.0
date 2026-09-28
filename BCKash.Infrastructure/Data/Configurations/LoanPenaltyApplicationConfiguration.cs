using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanPenaltyApplicationConfiguration : IEntityTypeConfiguration<LoanPenaltyApplication>
{
    public void Configure(EntityTypeBuilder<LoanPenaltyApplication> builder)
    {
        builder.ToTable("loan_penalty_applications");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.LoanId).HasColumnName("loan_id");
        builder.Property(p => p.ChargeId).HasColumnName("charge_id");
        builder.Property(p => p.ScheduleId).HasColumnName("schedule_id");
        builder.Property(p => p.Occurrence).HasColumnName("occurrence");
        builder.Property(p => p.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(p => p.LoanChargeId).HasColumnName("loan_charge_id");
        builder.Property(p => p.LoanTransactionId).HasColumnName("loan_transaction_id");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");

        // The guarantee that a penalty occurrence is only ever charged once, even if two runs overlap.
        builder.HasIndex(p => new { p.LoanId, p.ChargeId, p.ScheduleId, p.Occurrence })
            .IsUnique()
            .HasDatabaseName("loan_penalty_applications_occurrence_unique");
    }
}
