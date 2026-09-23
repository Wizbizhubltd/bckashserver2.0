using BCKash.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GlAccountConfiguration : IEntityTypeConfiguration<GlAccount>
{
    public void Configure(EntityTypeBuilder<GlAccount> builder)
    {
        builder.ToTable("gl_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(191);
        builder.Property(a => a.ParentId).HasColumnName("parent_id");
        builder.Property(a => a.GlCode).HasColumnName("gl_code").HasMaxLength(191);

        builder.Property(a => a.AccountType)
            .HasColumnName("account_type")
            .HasConversion(
                t => t == GlAccountType.Asset ? "asset"
                    : t == GlAccountType.Liability ? "liability"
                    : t == GlAccountType.Equity ? "equity"
                    : t == GlAccountType.Income ? "income"
                    : "expense",
                s => s == "asset" ? GlAccountType.Asset
                    : s == "liability" ? GlAccountType.Liability
                    : s == "equity" ? GlAccountType.Equity
                    : s == "income" ? GlAccountType.Income
                    : GlAccountType.Expense)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Active).HasColumnName("active").IsRequired();
        builder.Property(a => a.ManualEntries).HasColumnName("manual_entries").IsRequired();
        builder.Property(a => a.Notes).HasColumnName("notes");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(a => a.Parent)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
