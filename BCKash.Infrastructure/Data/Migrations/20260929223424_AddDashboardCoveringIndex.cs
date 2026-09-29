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
            // IF NOT EXISTS, not CreateIndex: building this on millions of rows can outlast the
            // client's command timeout, and MariaDB keeps building it after the client gives up —
            // so a retried migration must tolerate the index already being there.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_loan_repayment_schedules_dashboard_covering` " +
                "ON `loan_repayment_schedules` (`loan_id`, `due_date`, `principal`, `principal_paid`, `interest`, `interest_paid`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS `IX_loan_repayment_schedules_dashboard_covering` ON `loan_repayment_schedules`;");
        }
    }
}
