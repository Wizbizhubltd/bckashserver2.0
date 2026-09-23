using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanRescheduleRequestConfiguration : IEntityTypeConfiguration<LoanRescheduleRequest>
{
    public void Configure(EntityTypeBuilder<LoanRescheduleRequest> builder)
    {
        builder.ToTable("loan_reschedule_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.LoanId).HasColumnName("loan_id");
        builder.Property(r => r.Principal).HasColumnName("principal").HasPrecision(65, 4);

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v == RescheduleRequestStatus.Pending ? "pending" : v == RescheduleRequestStatus.Approved ? "approved" : "rejected",
                v => v == "pending" ? RescheduleRequestStatus.Pending : v == "approved" ? RescheduleRequestStatus.Approved : RescheduleRequestStatus.Rejected)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.CreatedById).HasColumnName("created_by_id");
        builder.Property(r => r.ModifiedById).HasColumnName("modified_by_id");
        builder.Property(r => r.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(r => r.RejectedById).HasColumnName("rejected_by_id");

        builder.Property(r => r.CreatedDate).HasColumnName("created_date").HasColumnType("date");
        builder.Property(r => r.ModifiedDate).HasColumnName("modified_date").HasColumnType("date");
        builder.Property(r => r.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(r => r.RejectedDate).HasColumnName("rejected_date").HasColumnType("date");
        builder.Property(r => r.RescheduleFromDate).HasColumnName("reschedule_from_date").HasColumnType("date");

        builder.Property(r => r.RecalculateInterest).HasColumnName("recalculate_interest").IsRequired();
        builder.Property(r => r.Notes).HasColumnName("notes");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(r => r.Loan)
            .WithMany(l => l.LoanRescheduleRequests)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
