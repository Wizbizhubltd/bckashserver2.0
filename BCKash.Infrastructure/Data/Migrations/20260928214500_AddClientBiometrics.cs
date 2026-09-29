using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientBiometrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "biometric_enrolled_at",
                table: "clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_biometrics",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: false),
                    purpose = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    session_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    liveness_confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    similarity = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    image_location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    failure_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_biometrics", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_client_biometrics_client_id_purpose_status",
                table: "client_biometrics",
                columns: new[] { "client_id", "purpose", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_client_biometrics_loan_id",
                table: "client_biometrics",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_biometrics_session_id",
                table: "client_biometrics",
                column: "session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_biometrics");

            migrationBuilder.DropColumn(
                name: "biometric_enrolled_at",
                table: "clients");
        }
    }
}
