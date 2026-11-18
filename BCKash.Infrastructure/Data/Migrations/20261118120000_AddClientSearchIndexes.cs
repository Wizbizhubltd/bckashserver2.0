using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Indexes for client search (start-of-name/number matching) and the newest-first client list per
    /// office, so both stay index lookups on a client base of hundreds of thousands.
    /// </summary>
    public partial class AddClientSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "IX_clients_first_name", table: "clients", column: "first_name");
            migrationBuilder.CreateIndex(name: "IX_clients_middle_name", table: "clients", column: "middle_name");
            migrationBuilder.CreateIndex(name: "IX_clients_display_name", table: "clients", column: "display_name");
            migrationBuilder.CreateIndex(name: "IX_clients_office_id_created_at", table: "clients", columns: new[] { "office_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_clients_first_name", table: "clients");
            migrationBuilder.DropIndex(name: "IX_clients_middle_name", table: "clients");
            migrationBuilder.DropIndex(name: "IX_clients_display_name", table: "clients");
            migrationBuilder.DropIndex(name: "IX_clients_office_id_created_at", table: "clients");
        }
    }
}
