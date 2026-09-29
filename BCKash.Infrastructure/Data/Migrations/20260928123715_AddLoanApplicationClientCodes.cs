using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanApplicationClientCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_application_client_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: false),
                    loan_product_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: false),
                    requested_by_id = table.Column<int>(type: "int", nullable: true),
                    code_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_to = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    loan_application_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_application_client_codes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_loan_application_client_codes_client_id_created_at_utc",
                table: "loan_application_client_codes",
                columns: new[] { "client_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_application_client_codes");
        }
    }
}
