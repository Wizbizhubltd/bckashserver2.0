using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PaymentTypeDetailConfiguration : IEntityTypeConfiguration<PaymentTypeDetail>
{
    public void Configure(EntityTypeBuilder<PaymentTypeDetail> builder)
    {
        builder.ToTable("payment_type_details");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(p => p.Type)
            .HasColumnName("type")
            .HasConversion(
                t => t == PaymentTypeDetailReferenceType.Loan ? "loan"
                    : t == PaymentTypeDetailReferenceType.Savings ? "savings"
                    : t == PaymentTypeDetailReferenceType.Share ? "share"
                    : t == PaymentTypeDetailReferenceType.Client ? "client"
                    : t == PaymentTypeDetailReferenceType.Journal ? "journal"
                    : null,
                s => s == "loan" ? PaymentTypeDetailReferenceType.Loan
                    : s == "savings" ? PaymentTypeDetailReferenceType.Savings
                    : s == "share" ? PaymentTypeDetailReferenceType.Share
                    : s == "client" ? PaymentTypeDetailReferenceType.Client
                    : s == "journal" ? PaymentTypeDetailReferenceType.Journal
                    : (PaymentTypeDetailReferenceType?)null)
            .HasMaxLength(20);

        builder.Property(p => p.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(p => p.AccountNumber).HasColumnName("account_number").HasMaxLength(191);
        builder.Property(p => p.ChequeNumber).HasColumnName("cheque_number").HasMaxLength(191);
        builder.Property(p => p.RoutingCode).HasColumnName("routing_code").HasMaxLength(191);
        builder.Property(p => p.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(191);
        builder.Property(p => p.Bank).HasColumnName("bank").HasMaxLength(191);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
    }
}
