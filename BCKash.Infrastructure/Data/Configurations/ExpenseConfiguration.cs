using BCKash.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OfficeId).HasColumnName("office_id");
        builder.Property(e => e.CreatedById).HasColumnName("created_by_id");
        builder.Property(e => e.ExpenseTypeId).HasColumnName("expense_type_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(65, 2).IsRequired();
        builder.Property(e => e.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(e => e.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(e => e.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(e => e.Recurring).HasColumnName("recurring").IsRequired();
        builder.Property(e => e.RecurFrequency).HasColumnName("recur_frequency").HasMaxLength(191).IsRequired();
        builder.Property(e => e.RecurStartDate).HasColumnName("recur_start_date").HasColumnType("date");
        builder.Property(e => e.RecurEndDate).HasColumnName("recur_end_date").HasColumnType("date");
        builder.Property(e => e.RecurNextDate).HasColumnName("recur_next_date").HasColumnType("date");

        builder.Property(e => e.RecurType)
            .HasColumnName("recur_type")
            .HasConversion(
                t => t == ExpenseRecurType.Day ? "day"
                    : t == ExpenseRecurType.Week ? "week"
                    : t == ExpenseRecurType.Month ? "month"
                    : "year",
                s => s == "day" ? ExpenseRecurType.Day
                    : s == "week" ? ExpenseRecurType.Week
                    : s == "month" ? ExpenseRecurType.Month
                    : ExpenseRecurType.Year)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == ApprovalStatus.Pending ? "pending" : s == ApprovalStatus.Approved ? "approved" : "declined",
                s => s == "pending" ? ApprovalStatus.Pending : s == "approved" ? ApprovalStatus.Approved : ApprovalStatus.Declined)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(e => e.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(e => e.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(e => e.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(e => e.Notes).HasColumnName("notes");
        builder.Property(e => e.Files).HasColumnName("files");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.ExpenseType)
            .WithMany(t => t.Expenses)
            .HasForeignKey(e => e.ExpenseTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
