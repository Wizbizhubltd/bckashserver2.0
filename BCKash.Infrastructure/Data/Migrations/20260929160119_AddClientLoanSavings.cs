using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLoanSavings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "savings_rate",
                table: "loans",
                type: "decimal(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_savings_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: false),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    loan_transaction_id = table.Column<int>(type: "int", nullable: true),
                    type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_savings_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_savings_entries_loan_transactions_loan_transaction_id",
                        column: x => x.loan_transaction_id,
                        principalTable: "loan_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_client_savings_entries_client_id",
                table: "client_savings_entries",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_savings_entries_loan_transaction_id",
                table: "client_savings_entries",
                column: "loan_transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_savings_entries");

            migrationBuilder.DropColumn(
                name: "savings_rate",
                table: "loans");
        }
    }
}
