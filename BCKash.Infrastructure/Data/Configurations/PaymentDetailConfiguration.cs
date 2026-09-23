using BCKash.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class PaymentDetailConfiguration : IEntityTypeConfiguration<PaymentDetail>
{
    public void Configure(EntityTypeBuilder<PaymentDetail> builder)
    {
        builder.ToTable("payment_details");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.PaymentTypeId).HasColumnName("payment_type_id");
        builder.Property(p => p.AccountNumber).HasColumnName("account_number").HasMaxLength(191);
        builder.Property(p => p.ChequeNumber).HasColumnName("cheque_number").HasMaxLength(191);
        builder.Property(p => p.RoutingCode).HasColumnName("routing_code").HasMaxLength(191);
        builder.Property(p => p.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(191);
        builder.Property(p => p.Bank).HasColumnName("bank").HasMaxLength(191);
        builder.Property(p => p.Notes).HasColumnName("notes");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.PaymentType)
            .WithMany(p => p.PaymentDetails)
            .HasForeignKey(p => p.PaymentTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
