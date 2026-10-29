using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeFundingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Funding acknowledgement became a module after roles were seeded: tick it for the manager role
            // (OfficePortalModules.Defaults) so branch managers can see their funding again. A fresh database
            // gets it from the bootstrap seeder instead.
            migrationBuilder.Sql(
                "INSERT IGNORE INTO role_modules (role_id, module) SELECT id, 'funding' FROM roles WHERE slug = 'manager';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_modules WHERE module = 'funding';");
        }
    }
}
