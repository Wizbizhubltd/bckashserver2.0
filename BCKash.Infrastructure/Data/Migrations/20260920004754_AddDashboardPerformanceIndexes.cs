using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_loans_status",
                table: "loans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_transaction_type_date",
                table: "loan_transactions",
                columns: new[] { "transaction_type", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayment_schedules_paid_due_date",
                table: "loan_repayment_schedules",
                columns: new[] { "paid", "due_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loans_status",
                table: "loans");

            migrationBuilder.DropIndex(
                name: "IX_loan_transactions_transaction_type_date",
                table: "loan_transactions");

            migrationBuilder.DropIndex(
                name: "IX_loan_repayment_schedules_paid_due_date",
                table: "loan_repayment_schedules");
        }
    }
}
