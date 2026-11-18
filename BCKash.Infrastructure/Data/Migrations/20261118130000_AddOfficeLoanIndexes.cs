using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Office staff only see their own office's loans and transactions, so the office portal's loan
    /// screens and dashboard filter on office first. Without these, each of those queries read every
    /// loan (hundreds of thousands) — and every loan transaction through the join.
    /// </summary>
    public partial class AddOfficeLoanIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "IX_loans_office_id_status", table: "loans", columns: new[] { "office_id", "status" });
            migrationBuilder.CreateIndex(name: "IX_loan_transactions_office_id", table: "loan_transactions", column: "office_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_loans_office_id_status", table: "loans");
            migrationBuilder.DropIndex(name: "IX_loan_transactions_office_id", table: "loan_transactions");
        }
    }
}
