using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase8PayrollAssetsExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "payroll",
                type: "decimal(65,2)",
                precision: 65,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "approved_by_id",
                table: "expense_budgets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "approved_date",
                table: "expense_budgets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "declined_by_id",
                table: "expense_budgets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "declined_date",
                table: "expense_budgets",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gross_amount",
                table: "payroll");

            migrationBuilder.DropColumn(
                name: "approved_by_id",
                table: "expense_budgets");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "expense_budgets");

            migrationBuilder.DropColumn(
                name: "declined_by_id",
                table: "expense_budgets");

            migrationBuilder.DropColumn(
                name: "declined_date",
                table: "expense_budgets");
        }
    }
}
