using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanNotificationConfiguration : IEntityTypeConfiguration<LoanNotification>
{
    public void Configure(EntityTypeBuilder<LoanNotification> builder)
    {
        builder.ToTable("loan_notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(n => n.LoanId).HasColumnName("loan_id");
        builder.Property(n => n.ScheduleId).HasColumnName("schedule_id");
        builder.Property(n => n.Kind).HasColumnName("kind").HasMaxLength(30).IsRequired();
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(n => new { n.LoanId, n.Kind, n.ScheduleId });
    }
}
