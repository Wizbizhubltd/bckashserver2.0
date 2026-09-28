using BCKash.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class LoanApplicationClientCodeConfiguration : IEntityTypeConfiguration<LoanApplicationClientCode>
{
    public void Configure(EntityTypeBuilder<LoanApplicationClientCode> builder)
    {
        builder.ToTable("loan_application_client_codes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.LoanProductId).HasColumnName("loan_product_id");
        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(65, 2);
        builder.Property(c => c.RequestedById).HasColumnName("requested_by_id");
        builder.Property(c => c.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.SentTo).HasColumnName("sent_to").HasMaxLength(191);
        builder.Property(c => c.Attempts).HasColumnName("attempts");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.ExpiresAtUtc).HasColumnName("expires_at_utc");
        // Concurrency token: marking a code used only succeeds if nobody else used it first, so two
        // submissions racing with the same code can't both create an application.
        builder.Property(c => c.ConsumedAtUtc).HasColumnName("consumed_at_utc").IsConcurrencyToken();
        builder.Property(c => c.LoanApplicationId).HasColumnName("loan_application_id");

        // Throttling looks up a client's recent codes.
        builder.HasIndex(c => new { c.ClientId, c.CreatedAtUtc });
    }
}
