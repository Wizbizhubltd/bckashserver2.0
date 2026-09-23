using BCKash.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PayrollConfiguration : IEntityTypeConfiguration<Payroll>
{
    public void Configure(EntityTypeBuilder<Payroll> builder)
    {
        builder.ToTable("payroll");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.PayrollTemplateId).HasColumnName("payroll_template_id");
        builder.Property(p => p.GlAccountExpenseId).HasColumnName("gl_account_expense_id");
        builder.Property(p => p.GlAccountAssetId).HasColumnName("gl_account_asset_id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.OfficeId).HasColumnName("office_id");
        builder.Property(p => p.EmployeeName).HasColumnName("employee_name").HasMaxLength(191);
        builder.Property(p => p.BusinessName).HasColumnName("business_name").HasMaxLength(191);
        builder.Property(p => p.PaymentMethod).HasColumnName("payment_method").HasMaxLength(191);
        builder.Property(p => p.PaymentTypeId).HasColumnName("payment_type_id").HasMaxLength(191);
        builder.Property(p => p.BankName).HasColumnName("bank_name").HasMaxLength(191);
        builder.Property(p => p.AccountNumber).HasColumnName("account_number").HasMaxLength(191);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(191);
        builder.Property(p => p.Comments).HasColumnName("comments");
        builder.Property(p => p.GrossAmount).HasColumnName("gross_amount").HasPrecision(65, 2).IsRequired();
        builder.Property(p => p.PaidAmount).HasColumnName("paid_amount").HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(p => p.Year).HasColumnName("year").HasMaxLength(191);
        builder.Property(p => p.Month).HasColumnName("month").HasMaxLength(191);
        builder.Property(p => p.Recurring).HasColumnName("recurring").IsRequired();
        builder.Property(p => p.RecurFrequency).HasColumnName("recur_frequency").HasMaxLength(191).IsRequired();
        builder.Property(p => p.RecurStartDate).HasColumnName("recur_start_date").HasColumnType("date");
        builder.Property(p => p.RecurEndDate).HasColumnName("recur_end_date").HasColumnType("date");
        builder.Property(p => p.RecurNextDate).HasColumnName("recur_next_date").HasColumnType("date");

        builder.Property(p => p.RecurType)
            .HasColumnName("recur_type")
            .HasConversion(
                t => t == PayrollRecurType.Days ? "days"
                    : t == PayrollRecurType.Weeks ? "weeks"
                    : t == PayrollRecurType.Months ? "months"
                    : "years",
                s => s == "days" ? PayrollRecurType.Days
                    : s == "weeks" ? PayrollRecurType.Weeks
                    : s == "months" ? PayrollRecurType.Months
                    : PayrollRecurType.Years)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.PayrollTemplate)
            .WithMany(t => t.Payrolls)
            .HasForeignKey(p => p.PayrollTemplateId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
