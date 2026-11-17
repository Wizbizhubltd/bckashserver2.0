using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <summary>Who last reassigned a client's or group's marketer, and when (see StaffAssignmentInterceptor).</summary>
    public partial class AddStaffReassignmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "staff_assigned_by_id",
                table: "clients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "staff_assigned_at",
                table: "clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "staff_assigned_by_id",
                table: "groups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "staff_assigned_at",
                table: "groups",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "staff_assigned_by_id", table: "clients");
            migrationBuilder.DropColumn(name: "staff_assigned_at", table: "clients");
            migrationBuilder.DropColumn(name: "staff_assigned_by_id", table: "groups");
            migrationBuilder.DropColumn(name: "staff_assigned_at", table: "groups");
        }
    }
}
