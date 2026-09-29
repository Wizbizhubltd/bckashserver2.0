using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficePortalRoleModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_modules",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false),
                    module = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_modules", x => new { x.role_id, x.module });
                    table.ForeignKey(
                        name: "FK_role_modules_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Roles seeded before modules existed start with the defaults (OfficePortalModules.Defaults);
            // on a fresh database the roles don't exist yet and the bootstrap seeder ticks them instead.
            TickModules(migrationBuilder, "director", "offices", "staff", "clients", "loans");
            TickModules(migrationBuilder, "controller", "staff", "clients", "loans");
            TickModules(migrationBuilder, "manager", "staff", "clients", "loans");
            TickModules(migrationBuilder, "marketer", "clients", "loans");
        }

        private static void TickModules(MigrationBuilder migrationBuilder, string roleSlug, params string[] modules)
        {
            foreach (var module in modules)
            {
                migrationBuilder.Sql($"INSERT INTO role_modules (role_id, module) SELECT id, '{module}' FROM roles WHERE slug = '{roleSlug}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_modules");
        }
    }
}
