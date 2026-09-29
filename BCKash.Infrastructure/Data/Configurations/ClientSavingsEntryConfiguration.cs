using BCKash.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientSavingsEntryConfiguration : IEntityTypeConfiguration<ClientSavingsEntry>
{
    public void Configure(EntityTypeBuilder<ClientSavingsEntry> builder)
    {
        builder.ToTable("client_savings_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ClientId).HasColumnName("client_id");
        builder.Property(e => e.LoanId).HasColumnName("loan_id");
        builder.Property(e => e.LoanTransactionId).HasColumnName("loan_transaction_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(65, 4);
        builder.Property(e => e.Notes).HasColumnName("notes");
        builder.Property(e => e.CreatedById).HasColumnName("created_by_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.LoanTransaction).WithMany().HasForeignKey(e => e.LoanTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ClientId);
    }
}
