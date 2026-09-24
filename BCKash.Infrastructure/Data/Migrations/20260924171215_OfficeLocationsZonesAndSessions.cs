using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OfficeLocationsZonesAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_device_id",
                table: "users",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "active_session_id",
                table: "users",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "persistences",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "session_id",
                table: "persistences",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "city_id",
                table: "offices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "created_by_id",
                table: "offices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lga_id",
                table: "offices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "office_code",
                table: "offices",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "state_id",
                table: "offices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zone_id",
                table: "offices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "states",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_states", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "lgas",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    state_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lgas", x => x.id);
                    table.ForeignKey(
                        name: "FK_lgas_states_state_id",
                        column: x => x.state_id,
                        principalTable: "states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    lga_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.id);
                    table.ForeignKey(
                        name: "FK_cities_lgas_lga_id",
                        column: x => x.lga_id,
                        principalTable: "lgas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_offices_city_id",
                table: "offices",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "IX_offices_lga_id",
                table: "offices",
                column: "lga_id");

            migrationBuilder.CreateIndex(
                name: "IX_offices_state_id",
                table: "offices",
                column: "state_id");

            migrationBuilder.CreateIndex(
                name: "IX_offices_zone_id",
                table: "offices",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "offices_office_code_unique",
                table: "offices",
                column: "office_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "cities_lga_id_name_unique",
                table: "cities",
                columns: new[] { "lga_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "lgas_state_id_name_unique",
                table: "lgas",
                columns: new[] { "state_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "states_name_unique",
                table: "states",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "zones_name_unique",
                table: "zones",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_offices_cities_city_id",
                table: "offices",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offices_lgas_lga_id",
                table: "offices",
                column: "lga_id",
                principalTable: "lgas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offices_states_state_id",
                table: "offices",
                column: "state_id",
                principalTable: "states",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_offices_zones_zone_id",
                table: "offices",
                column: "zone_id",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_offices_cities_city_id",
                table: "offices");

            migrationBuilder.DropForeignKey(
                name: "FK_offices_lgas_lga_id",
                table: "offices");

            migrationBuilder.DropForeignKey(
                name: "FK_offices_states_state_id",
                table: "offices");

            migrationBuilder.DropForeignKey(
                name: "FK_offices_zones_zone_id",
                table: "offices");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "zones");

            migrationBuilder.DropTable(
                name: "lgas");

            migrationBuilder.DropTable(
                name: "states");

            migrationBuilder.DropIndex(
                name: "IX_offices_city_id",
                table: "offices");

            migrationBuilder.DropIndex(
                name: "IX_offices_lga_id",
                table: "offices");

            migrationBuilder.DropIndex(
                name: "IX_offices_state_id",
                table: "offices");

            migrationBuilder.DropIndex(
                name: "IX_offices_zone_id",
                table: "offices");

            migrationBuilder.DropIndex(
                name: "offices_office_code_unique",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "active_device_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "active_session_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "users");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "persistences");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "persistences");

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "lga_id",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "office_code",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "state_id",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "offices");
        }
    }
}
