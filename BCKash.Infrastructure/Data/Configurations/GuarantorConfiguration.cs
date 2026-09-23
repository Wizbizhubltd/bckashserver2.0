using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class GuarantorConfiguration : IEntityTypeConfiguration<Guarantor>
{
    public void Configure(EntityTypeBuilder<Guarantor> builder)
    {
        builder.ToTable("guarantors");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(g => g.CountryId).HasColumnName("country_id");
        builder.Property(g => g.ClientId).HasColumnName("client_id");
        builder.Property(g => g.SavingsId).HasColumnName("savings_id");
        builder.Property(g => g.LoanId).HasColumnName("loan_id");
        builder.Property(g => g.LoanApplicationId).HasColumnName("loan_application_id");
        builder.Property(g => g.IsClient).HasColumnName("is_client").IsRequired();
        builder.Property(g => g.ClientRelationshipId).HasColumnName("client_relationship_id");
        builder.Property(g => g.Amount).HasColumnName("amount").HasPrecision(65, 4);

        builder.Property(g => g.Title).HasColumnName("title").HasMaxLength(191);
        builder.Property(g => g.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(g => g.MiddleName).HasColumnName("middle_name").HasMaxLength(191);
        builder.Property(g => g.LastName).HasColumnName("last_name").HasMaxLength(191);

        builder.Property(g => g.Gender)
            .HasColumnName("gender")
            .HasConversion(
                v => v == Gender.Male ? "male" : v == Gender.Female ? "female" : v == Gender.Other ? "other" : "unspecified",
                v => v == "male" ? Gender.Male : v == "female" ? Gender.Female : v == "other" ? Gender.Other : Gender.Unspecified)
            .HasMaxLength(20);

        builder.Property(g => g.Dob).HasColumnName("dob").HasColumnType("date");
        builder.Property(g => g.Street).HasColumnName("street").HasMaxLength(191);
        builder.Property(g => g.Address).HasColumnName("address");
        builder.Property(g => g.Mobile).HasColumnName("mobile").HasMaxLength(191);
        builder.Property(g => g.Phone).HasColumnName("phone").HasMaxLength(191);
        builder.Property(g => g.Email).HasColumnName("email").HasMaxLength(191);
        builder.Property(g => g.Picture).HasColumnName("picture");
        builder.Property(g => g.Work).HasColumnName("work").HasMaxLength(191);
        builder.Property(g => g.WorkAddress).HasColumnName("work_address");
        builder.Property(g => g.LockFunds).HasColumnName("lock_funds").IsRequired();

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(g => g.LoanId);
        builder.HasIndex(g => g.LoanApplicationId);

        builder.HasOne(g => g.Loan)
            .WithMany(l => l.Guarantors)
            .HasForeignKey(g => g.LoanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(g => g.LoanApplication)
            .WithMany(a => a.Guarantors)
            .HasForeignKey(g => g.LoanApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
