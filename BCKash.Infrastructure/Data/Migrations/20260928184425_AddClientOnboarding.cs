using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "group_clients",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "bvn_details_source",
                table: "clients",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "bvn_verified_at",
                table: "clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "high_risk_cleared_at",
                table: "clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "high_risk_cleared_by_id",
                table: "clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "high_risk_cleared_note",
                table: "clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "high_risk_flagged_at",
                table: "clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "high_risk_flagged_by_id",
                table: "clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "high_risk_reason",
                table: "clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_high_risk",
                table: "clients",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "entity_id",
                table: "audit_trail",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bvn_verifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    bvn = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    found = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    birth_date = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_by_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bvn_verifications", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "deletion_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    entity_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<int>(type: "int", nullable: false),
                    entity_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_by_id = table.Column<int>(type: "int", nullable: true),
                    reviewed_by_id = table.Column<int>(type: "int", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_note = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deletion_requests", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_module_entity_id",
                table: "audit_trail",
                columns: new[] { "module", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bvn_verifications_bvn",
                table: "bvn_verifications",
                column: "bvn");

            migrationBuilder.CreateIndex(
                name: "IX_deletion_requests_entity_type_entity_id",
                table: "deletion_requests",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_deletion_requests_status",
                table: "deletion_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bvn_verifications");

            migrationBuilder.DropTable(
                name: "deletion_requests");

            migrationBuilder.DropIndex(
                name: "IX_audit_trail_module_entity_id",
                table: "audit_trail");

            migrationBuilder.DropColumn(
                name: "role",
                table: "group_clients");

            migrationBuilder.DropColumn(
                name: "bvn_details_source",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "bvn_verified_at",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_cleared_at",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_cleared_by_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_cleared_note",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_flagged_at",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_flagged_by_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "high_risk_reason",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "is_high_risk",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "audit_trail");
        }
    }
}
