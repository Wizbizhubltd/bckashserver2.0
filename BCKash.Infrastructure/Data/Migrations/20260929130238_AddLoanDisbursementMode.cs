using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDisbursementMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "disbursement_account_name",
                table: "loans",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_account_number",
                table: "loans",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_bank_name",
                table: "loans",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_mode",
                table: "loans",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_account_name",
                table: "loan_applications",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_account_number",
                table: "loan_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_bank_name",
                table: "loan_applications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "disbursement_mode",
                table: "loan_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disbursement_account_name",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "disbursement_account_number",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "disbursement_bank_name",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "disbursement_mode",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "disbursement_account_name",
                table: "loan_applications");

            migrationBuilder.DropColumn(
                name: "disbursement_account_number",
                table: "loan_applications");

            migrationBuilder.DropColumn(
                name: "disbursement_bank_name",
                table: "loan_applications");

            migrationBuilder.DropColumn(
                name: "disbursement_mode",
                table: "loan_applications");
        }
    }
}
