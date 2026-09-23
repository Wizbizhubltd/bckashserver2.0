using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCKash.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.LegacyClientId).HasColumnName("client_id");
        builder.Property(c => c.Bvn).HasColumnName("bvn").HasMaxLength(255);
        builder.Property(c => c.CountryId).HasColumnName("country_id");
        builder.Property(c => c.OfficeId).HasColumnName("office_id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.StaffId).HasColumnName("staff_id");
        builder.Property(c => c.ReferredById).HasColumnName("referred_by_id");
        builder.Property(c => c.AccountNo).HasColumnName("account_no").HasMaxLength(191);
        builder.Property(c => c.OldAccountNo).HasColumnName("old_account_no").HasMaxLength(191);
        builder.Property(c => c.ExternalId).HasColumnName("external_id").HasMaxLength(191);
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(191);
        builder.Property(c => c.FirstName).HasColumnName("first_name").HasMaxLength(191);
        builder.Property(c => c.MiddleName).HasColumnName("middle_name").HasMaxLength(191);
        builder.Property(c => c.LastName).HasColumnName("last_name").HasMaxLength(191);
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(191);
        builder.Property(c => c.IncorporationNumber).HasColumnName("incorporation_number").HasMaxLength(191);
        builder.Property(c => c.DisplayName).HasColumnName("display_name").HasMaxLength(191);
        builder.Property(c => c.Picture).HasColumnName("picture").HasMaxLength(191);
        builder.Property(c => c.Mobile).HasColumnName("mobile").HasMaxLength(191);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(191);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(191);

        builder.Property(c => c.Gender)
            .HasColumnName("gender")
            .HasConversion(
                g => g == Gender.Male ? "male" : g == Gender.Female ? "female" : g == Gender.Other ? "other" : g == Gender.Unspecified ? "unspecified" : null,
                s => s == "male" ? Gender.Male : s == "female" ? Gender.Female : s == "other" ? Gender.Other : s == "unspecified" ? Gender.Unspecified : (Gender?)null)
            .HasMaxLength(20);

        builder.Property(c => c.ClientType)
            .HasColumnName("client_type")
            .HasConversion(
                t => t == ClientType.Individual ? "individual" : t == ClientType.Business ? "business" : t == ClientType.Ngo ? "ngo" : t == ClientType.Other ? "other" : null,
                s => s == "individual" ? ClientType.Individual : s == "business" ? ClientType.Business : s == "ngo" ? ClientType.Ngo : s == "other" ? ClientType.Other : (ClientType?)null)
            .HasMaxLength(20);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s == ClientStatus.Pending ? "pending" : s == ClientStatus.Active ? "active" : s == ClientStatus.Inactive ? "inactive" : s == ClientStatus.Declined ? "declined" : "closed",
                s => s == "pending" ? ClientStatus.Pending : s == "active" ? ClientStatus.Active : s == "inactive" ? ClientStatus.Inactive : s == "declined" ? ClientStatus.Declined : ClientStatus.Closed)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.MaritalStatus)
            .HasColumnName("marital_status")
            .HasConversion(
                m => m == MaritalStatus.Married ? "married" : m == MaritalStatus.Single ? "single" : m == MaritalStatus.Divorced ? "divorced" : m == MaritalStatus.Widowed ? "widowed" : m == MaritalStatus.Unspecified ? "unspecified" : null,
                s => s == "married" ? MaritalStatus.Married : s == "single" ? MaritalStatus.Single : s == "divorced" ? MaritalStatus.Divorced : s == "widowed" ? MaritalStatus.Widowed : s == "unspecified" ? MaritalStatus.Unspecified : (MaritalStatus?)null)
            .HasMaxLength(20);

        builder.Property(c => c.Dob).HasColumnName("dob").HasColumnType("date");
        builder.Property(c => c.Street).HasColumnName("street").HasMaxLength(191);
        builder.Property(c => c.Ward).HasColumnName("ward").HasMaxLength(191);
        builder.Property(c => c.District).HasColumnName("district").HasMaxLength(191);
        builder.Property(c => c.Region).HasColumnName("region").HasMaxLength(191);
        builder.Property(c => c.Address).HasColumnName("address");
        builder.Property(c => c.JoinedDate).HasColumnName("joined_date").HasColumnType("date");
        builder.Property(c => c.ActivatedDate).HasColumnName("activated_date").HasColumnType("date");
        builder.Property(c => c.ReactivatedDate).HasColumnName("reactivated_date").HasColumnType("date");
        builder.Property(c => c.DeclinedDate).HasColumnName("declined_date").HasColumnType("date");
        builder.Property(c => c.DeclinedReason).HasColumnName("declined_reason");
        builder.Property(c => c.ClosedReason).HasColumnName("closed_reason");
        builder.Property(c => c.ClosedDate).HasColumnName("closed_date").HasColumnType("date");
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.InactiveReason).HasColumnName("inactive_reason");
        builder.Property(c => c.InactiveDate).HasColumnName("inactive_date").HasColumnType("date");
        builder.Property(c => c.InactiveById).HasColumnName("inactive_by_id");
        builder.Property(c => c.ActivatedById).HasColumnName("activated_by_id");
        builder.Property(c => c.ReactivatedById).HasColumnName("reactivated_by_id");
        builder.Property(c => c.DeclinedById).HasColumnName("declined_by_id");
        builder.Property(c => c.ClosedById).HasColumnName("closed_by_id");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.Occupation).HasColumnName("occupation").HasMaxLength(191);
        builder.Property(c => c.PostalCode).HasColumnName("postal_code").HasMaxLength(191);
        builder.Property(c => c.Country).HasColumnName("country").HasMaxLength(191);
        builder.Property(c => c.State).HasColumnName("state").HasMaxLength(191);
        builder.Property(c => c.City).HasColumnName("city").HasMaxLength(191);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(c => c.LegacyClientId);

        // New in Phase 2 — the legacy DB never enforced account-number uniqueness (see
        // ClientAccountNumberFormat/ClientService); Bvn/Mobile/Status/OfficeId/StaffId and the
        // (LastName, FirstName) pair support FR-CLI-5's search/filter screen.
        builder.HasIndex(c => c.AccountNo).IsUnique();
        builder.HasIndex(c => c.Bvn);
        builder.HasIndex(c => c.Mobile);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.OfficeId);
        builder.HasIndex(c => c.StaffId);
        builder.HasIndex(c => new { c.LastName, c.FirstName });

        builder.HasOne(c => c.Office)
            .WithMany()
            .HasForeignKey(c => c.OfficeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
