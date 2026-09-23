using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanProvisioningCriteriaConfiguration : IEntityTypeConfiguration<LoanProvisioningCriteria>
{
    public void Configure(EntityTypeBuilder<LoanProvisioningCriteria> builder)
    {
        builder.ToTable("loan_provisioning_criteria");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.Min).HasColumnName("min");
        builder.Property(c => c.Max).HasColumnName("max");
        builder.Property(c => c.Percentage).HasColumnName("percentage");
        builder.Property(c => c.GlAccountLiabilityId).HasColumnName("gl_account_liability_id");
        builder.Property(c => c.GlAccountExpenseId).HasColumnName("gl_account_expense_id");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.Active).HasColumnName("active").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
    }
}
