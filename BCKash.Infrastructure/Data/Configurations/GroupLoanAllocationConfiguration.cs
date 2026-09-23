using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GroupLoanAllocationConfiguration : IEntityTypeConfiguration<GroupLoanAllocation>
{
    public void Configure(EntityTypeBuilder<GroupLoanAllocation> builder)
    {
        builder.ToTable("group_loan_allocation");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(g => g.LoanId).HasColumnName("loan_id");
        builder.Property(g => g.GroupId).HasColumnName("group_id");
        builder.Property(g => g.ClientId).HasColumnName("client_id");
        builder.Property(g => g.Amount).HasColumnName("amount").HasPrecision(65, 4);

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(g => g.Loan)
            .WithMany(l => l.GroupLoanAllocations)
            .HasForeignKey(g => g.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
