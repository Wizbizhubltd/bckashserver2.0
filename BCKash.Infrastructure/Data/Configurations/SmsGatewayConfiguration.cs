using BCKash.Domain.Communications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class SmsGatewayConfiguration : IEntityTypeConfiguration<SmsGateway>
{
    public void Configure(EntityTypeBuilder<SmsGateway> builder)
    {
        builder.ToTable("sms_gateways");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(g => g.CreatedById).HasColumnName("created_by_id");
        builder.Property(g => g.Name).HasColumnName("name");
        builder.Property(g => g.FromName).HasColumnName("from_name");
        builder.Property(g => g.ToName).HasColumnName("to_name");
        builder.Property(g => g.Url).HasColumnName("url");
        builder.Property(g => g.MsgName).HasColumnName("msg_name");
        builder.Property(g => g.Notes).HasColumnName("notes");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
    }
}
