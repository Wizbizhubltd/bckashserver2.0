using BCKash.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ReportSchedulerRunHistoryConfiguration : IEntityTypeConfiguration<ReportSchedulerRunHistory>
{
    public void Configure(EntityTypeBuilder<ReportSchedulerRunHistory> builder)
    {
        builder.ToTable("report_scheduler_run_history");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(h => h.ReportScheduleId).HasColumnName("report_schedule_id");
        builder.Property(h => h.ReportStartDate).HasColumnName("report_start_date").HasColumnType("date");
        builder.Property(h => h.ReportStartTime).HasColumnName("report_start_time").HasMaxLength(191);
        builder.Property(h => h.Notes).HasColumnName("notes");
        builder.Property(h => h.CreatedAt).HasColumnName("created_at");
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(h => h.ReportSchedule)
            .WithMany(r => r.RunHistory)
            .HasForeignKey(h => h.ReportScheduleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
