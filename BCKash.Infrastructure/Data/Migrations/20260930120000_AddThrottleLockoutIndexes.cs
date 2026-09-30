using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThrottleLockoutIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS, not CreateIndex: same reasoning as AddDashboardCoveringIndex — a retried
            // migration must tolerate an index a timed-out earlier attempt already finished building.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_throttle_user_id_type_created_at` ON `throttle` (`user_id`, `type`, `created_at`);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_throttle_ip_type_created_at` ON `throttle` (`ip`, `type`, `created_at`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_throttle_ip_type_created_at` ON `throttle`;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_throttle_user_id_type_created_at` ON `throttle`;");
        }
    }
}
