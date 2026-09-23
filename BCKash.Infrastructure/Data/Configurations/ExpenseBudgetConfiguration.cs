using BCKash.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ExpenseBudgetConfiguration : IEntityTypeConfiguration<ExpenseBudget>
{
    public void Configure(EntityTypeBuilder<ExpenseBudget> builder)
    {
        builder.ToTable("expense_budgets");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(b => b.CreatedById).HasColumnName("created_by_id");
        builder.Property(b => b.OfficeId).HasColumnName("office_id");
        builder.Property(b => b.ExpenseTypeId).HasColumnName("expense_type_id");
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(b => b.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(b => b.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(b => b.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(b => b.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(b => b.Notes).HasColumnName("notes");

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == ApprovalStatus.Pending ? "pending" : s == ApprovalStatus.Approved ? "approved" : "declined",
                s => s == "pending" ? ApprovalStatus.Pending : s == "approved" ? ApprovalStatus.Approved : ApprovalStatus.Declined)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(b => b.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(b => b.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(b => b.DeclinedById).HasColumnName("declined_by_id");

        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(b => b.ExpenseType)
            .WithMany(t => t.ExpenseBudgets)
            .HasForeignKey(b => b.ExpenseTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
