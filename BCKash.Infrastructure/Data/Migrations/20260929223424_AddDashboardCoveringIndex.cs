using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_loan_repayment_schedules_dashboard_covering",
                table: "loan_repayment_schedules",
                columns: new[] { "loan_id", "due_date", "principal", "principal_paid", "interest", "interest_paid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loan_repayment_schedules_dashboard_covering",
                table: "loan_repayment_schedules");
        }
    }
}
