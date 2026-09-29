using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanPenaltyApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_penalty_applications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: false),
                    charge_id = table.Column<int>(type: "int", nullable: false),
                    schedule_id = table.Column<int>(type: "int", nullable: false),
                    occurrence = table.Column<int>(type: "int", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: false),
                    loan_charge_id = table.Column<int>(type: "int", nullable: true),
                    loan_transaction_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_penalty_applications", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "loan_penalty_applications_occurrence_unique",
                table: "loan_penalty_applications",
                columns: new[] { "loan_id", "charge_id", "schedule_id", "occurrence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_penalty_applications");
        }
    }
}
