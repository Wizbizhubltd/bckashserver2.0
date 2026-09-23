using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixLateLoansIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loan_repayment_schedules_paid_due_date",
                table: "loan_repayment_schedules");

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayment_schedules_due_date",
                table: "loan_repayment_schedules",
                column: "due_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loan_repayment_schedules_due_date",
                table: "loan_repayment_schedules");

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayment_schedules_paid_due_date",
                table: "loan_repayment_schedules",
                columns: new[] { "paid", "due_date" });
        }
    }
}
