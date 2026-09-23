using BCKash.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class OtherIncomeConfiguration : IEntityTypeConfiguration<OtherIncome>
{
    public void Configure(EntityTypeBuilder<OtherIncome> builder)
    {
        builder.ToTable("other_income");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(o => o.OfficeId).HasColumnName("office_id");
        builder.Property(o => o.CreatedById).HasColumnName("created_by_id");
        builder.Property(o => o.OtherIncomeTypeId).HasColumnName("other_income_type_id");
        builder.Property(o => o.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(o => o.Amount).HasColumnName("amount").HasPrecision(65, 2).IsRequired();
        builder.Property(o => o.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(o => o.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(o => o.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(o => o.Notes).HasColumnName("notes");
        builder.Property(o => o.Files).HasColumnName("files");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == ApprovalStatus.Pending ? "pending" : s == ApprovalStatus.Approved ? "approved" : "declined",
                s => s == "pending" ? ApprovalStatus.Pending : s == "approved" ? ApprovalStatus.Approved : ApprovalStatus.Declined)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.ApprovedDate).HasColumnName("approved_date").HasColumnType("date");
        builder.Property(o => o.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(o => o.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(o => o.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(o => o.OtherIncomeType)
            .WithMany(t => t.OtherIncomes)
            .HasForeignKey(o => o.OtherIncomeTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
