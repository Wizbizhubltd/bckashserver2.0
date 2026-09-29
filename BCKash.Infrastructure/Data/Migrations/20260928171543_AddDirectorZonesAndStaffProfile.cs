using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorZonesAndStaffProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_account_name",
                table: "users",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "bank_account_number",
                table: "users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_of_kin_name",
                table: "users",
                type: "varchar(191)",
                maxLength: 191,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "next_of_kin_phone",
                table: "users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "next_of_kin_relationship",
                table: "users",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_zones",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    zone_id = table.Column<int>(type: "int", nullable: false),
                    assigned_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_zones", x => new { x.user_id, x.zone_id });
                    table.ForeignKey(
                        name: "FK_user_zones_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_zones_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_zones_zone_id",
                table: "user_zones",
                column: "zone_id");

            // Office-portal modules by default (see IdentityBootstrapSeeder.DefaultRolePermissions): the
            // seeder only seeds a role with no grants, so give roles seeded earlier the permissions
            // their modules need — managers manage staff; controllers and directors also work clients and loans.
            GrantToRole(migrationBuilder, "manager", "users.manage");
            GrantToRole(migrationBuilder, "controller", "clients.manage", "groups.manage", "loan-applications.manage", "loan-servicing.manage");
            GrantToRole(migrationBuilder, "director", "clients.manage", "groups.manage", "loan-applications.manage");
        }

        private static void GrantToRole(MigrationBuilder migrationBuilder, string roleSlug, params string[] permissionSlugs)
        {
            var slugList = string.Join(", ", permissionSlugs.Select(slug => $"'{slug}'"));
            migrationBuilder.Sql(
                $"INSERT INTO role_permissions (role_id, permission_id) " +
                $"SELECT r.id, p.id FROM roles r CROSS JOIN permissions p " +
                $"WHERE r.slug = '{roleSlug}' AND p.slug IN ({slugList}) " +
                $"AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_zones");

            migrationBuilder.DropColumn(
                name: "bank_account_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bank_account_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bank_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "next_of_kin_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "next_of_kin_phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "next_of_kin_relationship",
                table: "users");
        }
    }
}
