using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanFormFieldsAndApplicationFormFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "form_fee",
                table: "loan_applications",
                type: "decimal(65,4)",
                precision: 65,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "form_fee_charge_id",
                table: "loan_applications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_address",
                table: "clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "nationality",
                table: "clients",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "client_contacts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "photo",
                table: "client_contacts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // The paper form's "This form cost a non-refundable fee of N2000" — a default every loan
            // applicant pays, its amount changed afterwards in Settings → Fees & Payments.
            migrationBuilder.Sql(
                """
                INSERT INTO charges (name, product, charge_type, charge_option, charge_frequency, charge_frequency_type, charge_frequency_amount,
                                     amount, charge_payment_mode, active, penalty, `override`, created_at, updated_at)
                SELECT 'Loan application form fee', 'loan', 'application_form_fee', 'flat', 0, 'days', 0, 2000, 'regular', 1, 0, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                WHERE NOT EXISTS (SELECT 1 FROM charges WHERE charge_type = 'application_form_fee');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM charges WHERE charge_type = 'application_form_fee';");

            migrationBuilder.DropColumn(
                name: "form_fee",
                table: "loan_applications");

            migrationBuilder.DropColumn(
                name: "form_fee_charge_id",
                table: "loan_applications");

            migrationBuilder.DropColumn(
                name: "business_address",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "nationality",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "client_contacts");

            migrationBuilder.DropColumn(
                name: "photo",
                table: "client_contacts");
        }
    }
}
