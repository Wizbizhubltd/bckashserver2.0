using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffRecordIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_loans_loan_officer_id",
                table: "loans",
                column: "loan_officer_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_user_id",
                table: "audit_trail",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loans_loan_officer_id",
                table: "loans");

            migrationBuilder.DropIndex(
                name: "IX_audit_trail_user_id",
                table: "audit_trail");
        }
    }
}
