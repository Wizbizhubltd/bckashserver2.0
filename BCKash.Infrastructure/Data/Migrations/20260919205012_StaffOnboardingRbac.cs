using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffOnboardingRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "created_by_id",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "onboarding_approved_by_id",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "onboarding_approved_date",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "onboarding_declined_by_id",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "onboarding_declined_date",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "onboarding_declined_reason",
                table: "users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "onboarding_status",
                table: "users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "approved")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "user_class",
                table: "users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_approved_by_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_approved_date",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_declined_by_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_declined_date",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_declined_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarding_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_class",
                table: "users");
        }
    }
}
