using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargePenaltyControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "free_after_installments",
                table: "charges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "grace_days",
                table: "charges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_total_percent",
                table: "charges",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "repeat_every_days",
                table: "charges",
                type: "int",
                nullable: true);

            // Penalty status now follows from the charge type (ChargeRules) — correct legacy rows
            // such as late-payment fees that were saved without the penalty flag.
            migrationBuilder.Sql("UPDATE charges SET penalty = 1 WHERE charge_type IN ('overdue_installment_fee', 'overdue_maturity', 'early_repayment') AND penalty = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "free_after_installments",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "grace_days",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "max_total_percent",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "repeat_every_days",
                table: "charges");
        }
    }
}
