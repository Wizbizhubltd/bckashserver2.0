using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "asset_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_fixed_asset_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_asset_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_contra_asset_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_expense_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_liability_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "audit_trail",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    module = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_trail", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    product = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_option = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_frequency = table.Column<int>(type: "int", nullable: false),
                    charge_frequency_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_frequency_amount = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    minimum_amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    maximum_amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    charge_payment_mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    penalty = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    @override = table.Column<bool>(name: "override", type: "tinyint(1)", nullable: false),
                    gl_account_income_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charges", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_identification_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_identification_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_profession",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_profession", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_relationships",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_relationships", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "collateral_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collateral_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "communication_campaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_start_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recurrence_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_frequency = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_interval = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_recipients = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_subject = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_attachment_file_format = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recipients_category = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_attachment = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_day = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_day = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_officer_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    manual_entries = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_status = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_product_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_run_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_run_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_run_time = table.Column<DateOnly>(type: "date", nullable: true),
                    next_run_time = table.Column<DateOnly>(type: "date", nullable: true),
                    number_of_runs = table.Column<int>(type: "int", nullable: false),
                    number_of_recipients = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    sent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_campaigns", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    sortname = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    symbol = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decimals = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    xrate = table.Column<decimal>(type: "decimal(65,8)", precision: 65, scale: 8, nullable: true),
                    international_code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custom_fields",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    category = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    field_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    radio_box_values = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    checkbox_values = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    select_values = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_fields", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    record_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    size = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_asset_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_expense_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "funds",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funds", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gl_accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    gl_code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    manual_entries = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_gl_accounts_gl_accounts_parent_id",
                        column: x => x.parent_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gl_closures",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    closing_date = table.Column<DateOnly>(type: "date", nullable: false),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    gl_reference = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reopened_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reopened_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_closures", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    short_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fund_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    decimals = table.Column<int>(type: "int", nullable: false),
                    minimum_principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    default_principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    maximum_principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    minimum_loan_term = table.Column<int>(type: "int", nullable: true),
                    default_loan_term = table.Column<int>(type: "int", nullable: true),
                    maximum_loan_term = table.Column<int>(type: "int", nullable: true),
                    repayment_frequency = table.Column<int>(type: "int", nullable: true),
                    repayment_frequency_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    minimum_interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    default_interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    maximum_interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_rate_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    grace_on_interest_charged = table.Column<int>(type: "int", nullable: true),
                    grace_on_principal = table.Column<int>(type: "int", nullable: true),
                    grace_on_interest_payment = table.Column<int>(type: "int", nullable: true),
                    allow_custom_grace = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    allow_standing_instuctions = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    interest_method = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    armotization_method = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    interest_calculation_period_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year_days = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month_days = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_transaction_strategy = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    include_in_cycle = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    lock_guarantee = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    allocate_overpayments = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    allow_additional_charges = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    accounting_rule = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    npa_days = table.Column<int>(type: "int", nullable: true),
                    arrears_grace_days = table.Column<int>(type: "int", nullable: true),
                    npa_suspend_income = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    gl_account_fund_source_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_loan_portfolio_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_receivable_interest_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_receivable_fee_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_receivable_penalty_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_loan_over_payments_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_suspended_income_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_interest_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_fee_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_penalty_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_recovery_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_loans_written_off_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_products", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_provisioning_criteria",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    min = table.Column<int>(type: "int", nullable: true),
                    max = table.Column<int>(type: "int", nullable: true),
                    percentage = table.Column<int>(type: "int", nullable: true),
                    gl_account_liability_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_expense_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_provisioning_criteria", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_purposes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_purposes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    reference_id = table.Column<int>(type: "int", nullable: true),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "office_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    from_office_id = table.Column<int>(type: "int", nullable: true),
                    to_office_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,8)", precision: 65, scale: 8, nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_office_transactions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "offices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    external_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    opening_date = table.Column<DateOnly>(type: "date", nullable: true),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    manager_id = table.Column<int>(type: "int", nullable: true),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    default_office = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offices", x => x.id);
                    table.ForeignKey(
                        name: "FK_offices_offices_parent_id",
                        column: x => x.parent_id,
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "other_income_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_asset_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_other_income_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_type_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<int>(type: "int", nullable: false),
                    account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cheque_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    routing_code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receipt_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bank = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_type_details", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_cash = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_types", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payroll_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_templates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slug = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissions_permissions_parent_id",
                        column: x => x.parent_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "report_scheduler",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_start_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recurrence_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_frequency = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_interval = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_recipients = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_subject = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_attachment_file_format = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_category = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_name = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_date_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    office_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_officer_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    manual_entries = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_status = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_product_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_run_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_run_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_run_time = table.Column<DateOnly>(type: "date", nullable: true),
                    next_run_time = table.Column<DateOnly>(type: "date", nullable: true),
                    number_of_runs = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_scheduler", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    slug = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    time_limit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    from_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    access_days = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    permissions = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    setting_key = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    setting_value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sms_gateways",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    url = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    msg_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sms_gateways", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    asset_type_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: true),
                    purchase_price = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    life_span = table.Column<int>(type: "int", nullable: true),
                    salvage_value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    serial_number = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    files = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purchase_year = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_assets_asset_types_asset_type_id",
                        column: x => x.asset_type_id,
                        principalTable: "asset_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custom_field_values",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    custom_field_id = table.Column<int>(type: "int", nullable: false),
                    entity_type = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_field_values_custom_fields_custom_field_id",
                        column: x => x.custom_field_id,
                        principalTable: "custom_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custom_fields_meta",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    category = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    custom_field_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_fields_meta", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_fields_meta_custom_fields_custom_field_id",
                        column: x => x.custom_field_id,
                        principalTable: "custom_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_fields_meta_custom_fields_meta_parent_id",
                        column: x => x.parent_id,
                        principalTable: "custom_fields_meta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expense_budgets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    expense_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_budgets", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_budgets_expense_types_expense_type_id",
                        column: x => x.expense_type_id,
                        principalTable: "expense_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    expense_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    recur_frequency = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_next_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    files = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_expenses_expense_types_expense_type_id",
                        column: x => x.expense_type_id,
                        principalTable: "expense_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "savings_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    short_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    decimals = table.Column<int>(type: "int", nullable: false),
                    interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    allow_overdraft = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    minimum_balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_compounding_period = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    interest_posting_period = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    interest_calculation_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    allow_transfer_withdrawal_fee = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    opening_balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    allow_additional_charges = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    year_days = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    accounting_rule = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gl_account_savings_reference_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_overdraft_portfolio_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_savings_control_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_interest_on_savings_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_savings_written_off_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_interest_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_fee_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_income_penalty_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_income_fee_id",
                        column: x => x.gl_account_income_fee_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_income_interest_id",
                        column: x => x.gl_account_income_interest_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_income_penalty_id",
                        column: x => x.gl_account_income_penalty_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_interest_on_savings_~",
                        column: x => x.gl_account_interest_on_savings_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_overdraft_portfolio_~",
                        column: x => x.gl_account_overdraft_portfolio_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_savings_control_id",
                        column: x => x.gl_account_savings_control_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_savings_reference_id",
                        column: x => x.gl_account_savings_reference_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_savings_products_gl_accounts_gl_account_savings_written_off_~",
                        column: x => x.gl_account_savings_written_off_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_product_charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_product_id = table.Column<int>(type: "int", nullable: true),
                    charge_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_product_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_product_charges_loan_products_loan_product_id",
                        column: x => x.loan_product_id,
                        principalTable: "loan_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_product_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    old_client_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    fund_id = table.Column<int>(type: "int", nullable: true),
                    loan_purpose_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    decimals = table.Column<int>(type: "int", nullable: false),
                    account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    external_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_officer_id = table.Column<int>(type: "int", nullable: true),
                    principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    applied_amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    approved_amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    disbursement_fees = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    processing_fee = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    loan_term = table.Column<int>(type: "int", nullable: true),
                    loan_term_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    repayment_frequency = table.Column<int>(type: "int", nullable: true),
                    repayment_frequency_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    override_interest = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    override_interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_rate_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expected_disbursement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    disbursement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_maturity_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_first_repayment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    repayments_number = table.Column<int>(type: "int", nullable: true),
                    first_repayment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    interest_method = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    armotization_method = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    grace_on_interest_charged = table.Column<int>(type: "int", nullable: true),
                    grace_on_principal = table.Column<int>(type: "int", nullable: true),
                    grace_on_interest_payment = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    need_changes_by_id = table.Column<int>(type: "int", nullable: true),
                    withdrawn_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    written_off_by_id = table.Column<int>(type: "int", nullable: true),
                    disbursed_by_id = table.Column<int>(type: "int", nullable: true),
                    rescheduled_by_id = table.Column<int>(type: "int", nullable: true),
                    closed_by_id = table.Column<int>(type: "int", nullable: true),
                    created_date = table.Column<DateOnly>(type: "date", nullable: true),
                    modified_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    need_changes_date = table.Column<DateOnly>(type: "date", nullable: true),
                    withdrawn_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    written_off_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rescheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    declined_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    written_off_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    disbursed_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    withdrawn_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rescheduled_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_npa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    income_suspended = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.id);
                    table.ForeignKey(
                        name: "FK_loans_loan_products_loan_product_id",
                        column: x => x.loan_product_id,
                        principalTable: "loan_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loans_loan_purposes_loan_purpose_id",
                        column: x => x.loan_purpose_id,
                        principalTable: "loan_purposes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    old_group_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_no = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    external_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    staff_id = table.Column<int>(type: "int", nullable: true),
                    joined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    activated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reactivated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    inactive_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    inactive_date = table.Column<DateOnly>(type: "date", nullable: true),
                    inactive_by_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    activated_by_id = table.Column<int>(type: "int", nullable: true),
                    reactivated_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    closed_by_id = table.Column<int>(type: "int", nullable: true),
                    mobile = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    street = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ward = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    district = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    region = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_groups_offices_office_id",
                        column: x => x.office_id,
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    permissions = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_login = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enable_google2fa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    blocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    google2fa_secret = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    time_limit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    from_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    access_days = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_offices_office_id",
                        column: x => x.office_id,
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "other_income",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    other_income_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    files = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_other_income", x => x.id);
                    table.ForeignKey(
                        name: "FK_other_income_other_income_types_other_income_type_id",
                        column: x => x.other_income_type_id,
                        principalTable: "other_income_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_type_id = table.Column<int>(type: "int", nullable: true),
                    account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cheque_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    routing_code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receipt_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bank = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_details_payment_types_payment_type_id",
                        column: x => x.payment_type_id,
                        principalTable: "payment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payroll",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payroll_template_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_expense_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_asset_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    employee_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    business_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_method = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_type_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bank_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    comments = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paid_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    recur_frequency = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recur_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_next_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recur_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_payroll_templates_payroll_template_id",
                        column: x => x.payroll_template_id,
                        principalTable: "payroll_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payroll_template_meta",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payroll_template_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    position = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_default = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_tax = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_percentage = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tax_on = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    default_value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_template_meta", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_template_meta_payroll_templates_payroll_template_id",
                        column: x => x.payroll_template_id,
                        principalTable: "payroll_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "report_scheduler_run_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    report_schedule_id = table.Column<int>(type: "int", nullable: true),
                    report_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_start_time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_scheduler_run_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_scheduler_run_history_report_scheduler_report_schedul~",
                        column: x => x.report_schedule_id,
                        principalTable: "report_scheduler",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false),
                    permission_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "asset_depreciation",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    asset_id = table.Column<int>(type: "int", nullable: true),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    beginning_value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    depreciation_value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    cost = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    accumulated = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    ending_value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_depreciation", x => x.id);
                    table.ForeignKey(
                        name: "FK_asset_depreciation_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "savings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    client_id = table.Column<int>(type: "int", nullable: false),
                    old_client_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    field_officer_id = table.Column<int>(type: "int", nullable: true),
                    savings_product_id = table.Column<int>(type: "int", nullable: true),
                    external_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_account_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    decimals = table.Column<int>(type: "int", nullable: false),
                    interest_rate = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    allow_overdraft = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    minimum_balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    overdraft_limit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_compounding_period = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    interest_posting_period = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    allow_transfer_withdrawal_fee = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    opening_balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    allow_additional_charges = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    year_days = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    closed_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    created_date = table.Column<DateOnly>(type: "date", nullable: true),
                    modified_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    declined_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    deposits = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_earned = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_posted = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_overdraft = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    withdrawals = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    start_interest_calculation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_interest_calculation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_interest_calculation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_interest_posting_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_interest_posting_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_savings_products_savings_product_id",
                        column: x => x.savings_product_id,
                        principalTable: "savings_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "savings_product_charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    charge_id = table.Column<int>(type: "int", nullable: true),
                    savings_product_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    grace_period = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_product_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_product_charges_savings_products_savings_product_id",
                        column: x => x.savings_product_id,
                        principalTable: "savings_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "group_loan_allocation",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_loan_allocation", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_loan_allocation_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_applications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    loan_purpose_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    loan_product_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guarantor_ids = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_term = table.Column<int>(type: "int", nullable: true),
                    loan_term_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    declined_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_applications_loan_products_loan_product_id",
                        column: x => x.loan_product_id,
                        principalTable: "loan_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_applications_loan_purposes_loan_purpose_id",
                        column: x => x.loan_purpose_id,
                        principalTable: "loan_purposes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_applications_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    charge_id = table.Column<int>(type: "int", nullable: true),
                    penalty = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    waived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    charge_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_option = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    amount_paid = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    grace_period = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_charges_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_repayment_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    installment = table.Column<int>(type: "int", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal_waived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal_written_off = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal_paid = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_waived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_written_off = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_paid = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees_waived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees_written_off = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees_paid = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty_waived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty_written_off = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty_paid = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    total_due = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    total_paid_advance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    total_paid_late = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    paid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_repayment_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_repayment_schedules_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_reschedule_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    rejected_by_id = table.Column<int>(type: "int", nullable: true),
                    created_date = table.Column<DateOnly>(type: "date", nullable: true),
                    modified_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rejected_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reschedule_from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recalculate_interest = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_reschedule_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_reschedule_requests_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "activations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activations", x => x.id);
                    table.ForeignKey(
                        name: "FK_activations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    bvn = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    country_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    staff_id = table.Column<int>(type: "int", nullable: true),
                    referred_by_id = table.Column<int>(type: "int", nullable: true),
                    account_no = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_account_no = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    external_id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    incorporation_number = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    display_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mobile = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    client_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    marital_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    street = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ward = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    district = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    region = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    joined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    activated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reactivated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declined_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    inactive_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    inactive_date = table.Column<DateOnly>(type: "date", nullable: true),
                    inactive_by_id = table.Column<int>(type: "int", nullable: true),
                    activated_by_id = table.Column<int>(type: "int", nullable: true),
                    reactivated_by_id = table.Column<int>(type: "int", nullable: true),
                    declined_by_id = table.Column<int>(type: "int", nullable: true),
                    closed_by_id = table.Column<int>(type: "int", nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occupation = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    postal_code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    country = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    state = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    city = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.ForeignKey(
                        name: "FK_clients_offices_office_id",
                        column: x => x.office_id,
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "group_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_users_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "persistences",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persistences", x => x.id);
                    table.ForeignKey(
                        name: "FK_persistences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "role_users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_users", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_role_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "throttle",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    type = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ip = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_throttle", x => x.id);
                    table.ForeignKey(
                        name: "FK_throttle_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    permission_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => new { x.user_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_user_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payroll_meta",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payroll_id = table.Column<int>(type: "int", nullable: false),
                    payroll_template_meta_id = table.Column<int>(type: "int", nullable: true),
                    value = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    is_tax = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    is_percentage = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    position = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_meta", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_meta_payroll_payroll_id",
                        column: x => x.payroll_id,
                        principalTable: "payroll",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_meta_payroll_template_meta_payroll_template_meta_id",
                        column: x => x.payroll_template_meta_id,
                        principalTable: "payroll_template_meta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gl_journal_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    gl_account_id = table.Column<int>(type: "int", nullable: true),
                    currency_id = table.Column<int>(type: "int", nullable: true),
                    transaction_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    transaction_sub_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    debit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    credit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    reversed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    loan_transaction_id = table.Column<int>(type: "int", nullable: true),
                    savings_transaction_id = table.Column<int>(type: "int", nullable: true),
                    savings_id = table.Column<int>(type: "int", nullable: true),
                    shares_transaction_id = table.Column<int>(type: "int", nullable: true),
                    payroll_transaction_id = table.Column<int>(type: "int", nullable: true),
                    payment_detail_id = table.Column<int>(type: "int", nullable: true),
                    transaction_id = table.Column<int>(type: "int", nullable: true),
                    gl_closure_id = table.Column<int>(type: "int", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    narration = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    reconciled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    manual_entry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    approved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_gl_journal_entries_gl_accounts_gl_account_id",
                        column: x => x.gl_account_id,
                        principalTable: "gl_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_journal_entries_gl_closures_gl_closure_id",
                        column: x => x.gl_closure_id,
                        principalTable: "gl_closures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_journal_entries_savings_savings_id",
                        column: x => x.savings_id,
                        principalTable: "savings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "savings_charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    savings_id = table.Column<int>(type: "int", nullable: true),
                    charge_id = table.Column<int>(type: "int", nullable: true),
                    penalty = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    waived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    charge_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    charge_option = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    amount_paid = table.Column<decimal>(type: "decimal(65,2)", precision: 65, scale: 2, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    grace_period = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_charges_savings_savings_id",
                        column: x => x.savings_id,
                        principalTable: "savings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "savings_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    payment_detail_id = table.Column<int>(type: "int", nullable: true),
                    savings_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    debit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    credit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    transaction_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reversible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reversed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reversal_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    system_interest = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    time = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    balance_date = table.Column<DateOnly>(type: "date", nullable: true),
                    balance_days = table.Column<int>(type: "int", nullable: true),
                    cumulative_balance_days = table.Column<int>(type: "int", nullable: true),
                    cumulative_balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_transactions_savings_savings_id",
                        column: x => x.savings_id,
                        principalTable: "savings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "collateral",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    loan_application_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    collateral_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    serial = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    value = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gallery = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collateral", x => x.id);
                    table.ForeignKey(
                        name: "FK_collateral_collateral_types_collateral_type_id",
                        column: x => x.collateral_type_id,
                        principalTable: "collateral_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collateral_loan_applications_loan_application_id",
                        column: x => x.loan_application_id,
                        principalTable: "loan_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collateral_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "guarantors",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    country_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    savings_id = table.Column<int>(type: "int", nullable: true),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    loan_application_id = table.Column<int>(type: "int", nullable: true),
                    is_client = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    client_relationship_id = table.Column<int>(type: "int", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    title = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    street = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mobile = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    work = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    work_address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lock_funds = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantors", x => x.id);
                    table.ForeignKey(
                        name: "FK_guarantors_loan_applications_loan_application_id",
                        column: x => x.loan_application_id,
                        principalTable: "loan_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guarantors_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_id = table.Column<int>(type: "int", nullable: true),
                    office_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    payment_type_id = table.Column<int>(type: "int", nullable: true),
                    transaction_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    modified_by_id = table.Column<int>(type: "int", nullable: true),
                    payment_detail_id = table.Column<int>(type: "int", nullable: true),
                    charge_id = table.Column<int>(type: "int", nullable: true),
                    loan_repayment_schedule_id = table.Column<int>(type: "int", nullable: true),
                    debit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    credit = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    balance = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    reversible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reversed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reversal_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_apply_to = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_by_id = table.Column<int>(type: "int", nullable: true),
                    approved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    interest = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fee = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    overpayment = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    month = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receipt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    principal_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    interest_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fees_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    overpayment_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    unrecognized_income_derived = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_transactions_loan_repayment_schedules_loan_repayment_sc~",
                        column: x => x.loan_repayment_schedule_id,
                        principalTable: "loan_repayment_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_transactions_loans_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_identifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    client_identification_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<sbyte>(type: "tinyint(4)", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attachment = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_identifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_identifications_client_identification_types_client_id~",
                        column: x => x.client_identification_type_id,
                        principalTable: "client_identification_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_identifications_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_next_of_gaur",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    client_relationship_id = table.Column<int>(type: "int", nullable: true),
                    qualification = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ward = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    street = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    district = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    region = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mobile = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_next_of_gaur", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_next_of_gaur_client_relationships_client_relationship~",
                        column: x => x.client_relationship_id,
                        principalTable: "client_relationships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_next_of_gaur_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_next_of_kin",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    client_relationship_id = table.Column<int>(type: "int", nullable: true),
                    qualification = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ward = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    street = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    district = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    region = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    picture = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mobile = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_next_of_kin", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_next_of_kin_client_relationships_client_relationship_~",
                        column: x => x.client_relationship_id,
                        principalTable: "client_relationships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_next_of_kin_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_users_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "group_clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    client_id = table.Column<int>(type: "int", nullable: true),
                    old_group_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_client_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_id = table.Column<int>(type: "int", nullable: true),
                    removed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    removed_by_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_clients", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_clients_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_clients_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "loan_transaction_repayment_schedule_mappings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    loan_repayment_schedule_id = table.Column<int>(type: "int", nullable: true),
                    loan_transaction_id = table.Column<int>(type: "int", nullable: true),
                    interest = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    principal = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    fee = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    penalty = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    overpayment = table.Column<decimal>(type: "decimal(65,4)", precision: 65, scale: 4, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_transaction_repayment_schedule_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_transaction_repayment_schedule_mappings_loan_repayment_~",
                        column: x => x.loan_repayment_schedule_id,
                        principalTable: "loan_repayment_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_transaction_repayment_schedule_mappings_loan_transactio~",
                        column: x => x.loan_transaction_id,
                        principalTable: "loan_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_activations_user_id",
                table: "activations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_asset_depreciation_asset_id",
                table: "asset_depreciation",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_assets_asset_type_id",
                table: "assets",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_identifications_client_id",
                table: "client_identifications",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_identifications_client_identification_type_id",
                table: "client_identifications",
                column: "client_identification_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_next_of_gaur_client_id",
                table: "client_next_of_gaur",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_next_of_gaur_client_relationship_id",
                table: "client_next_of_gaur",
                column: "client_relationship_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_next_of_kin_client_id",
                table: "client_next_of_kin",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_next_of_kin_client_relationship_id",
                table: "client_next_of_kin",
                column: "client_relationship_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_users_client_id_user_id",
                table: "client_users",
                columns: new[] { "client_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_users_user_id",
                table: "client_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_account_no",
                table: "clients",
                column: "account_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_bvn",
                table: "clients",
                column: "bvn");

            migrationBuilder.CreateIndex(
                name: "IX_clients_client_id",
                table: "clients",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_last_name_first_name",
                table: "clients",
                columns: new[] { "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_mobile",
                table: "clients",
                column: "mobile");

            migrationBuilder.CreateIndex(
                name: "IX_clients_office_id",
                table: "clients",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_staff_id",
                table: "clients",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_status",
                table: "clients",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_clients_user_id",
                table: "clients",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_collateral_collateral_type_id",
                table: "collateral",
                column: "collateral_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_collateral_loan_application_id",
                table: "collateral",
                column: "loan_application_id");

            migrationBuilder.CreateIndex(
                name: "IX_collateral_loan_id",
                table: "collateral",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_custom_field_id",
                table: "custom_field_values",
                column: "custom_field_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_entity_type_entity_id",
                table: "custom_field_values",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_custom_fields_meta_custom_field_id",
                table: "custom_fields_meta",
                column: "custom_field_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_fields_meta_parent_id",
                table: "custom_fields_meta",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_type_record_id",
                table: "documents",
                columns: new[] { "type", "record_id" });

            migrationBuilder.CreateIndex(
                name: "IX_expense_budgets_expense_type_id",
                table: "expense_budgets",
                column: "expense_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_expense_type_id",
                table: "expenses",
                column: "expense_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_accounts_parent_id",
                table: "gl_accounts",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_closures_office_id_closing_date",
                table: "gl_closures",
                columns: new[] { "office_id", "closing_date" });

            migrationBuilder.CreateIndex(
                name: "IX_gl_journal_entries_gl_account_id",
                table: "gl_journal_entries",
                column: "gl_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_journal_entries_gl_closure_id",
                table: "gl_journal_entries",
                column: "gl_closure_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_journal_entries_office_id_date",
                table: "gl_journal_entries",
                columns: new[] { "office_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_gl_journal_entries_reference",
                table: "gl_journal_entries",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "IX_gl_journal_entries_savings_id",
                table: "gl_journal_entries",
                column: "savings_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_clients_client_id",
                table: "group_clients",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_clients_group_id",
                table: "group_clients",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_loan_allocation_loan_id",
                table: "group_loan_allocation",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_users_group_id",
                table: "group_users",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_users_user_id",
                table: "group_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_account_no",
                table: "groups",
                column: "account_no");

            migrationBuilder.CreateIndex(
                name: "IX_groups_name",
                table: "groups",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_groups_office_id",
                table: "groups",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_staff_id",
                table: "groups",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_status",
                table: "groups",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_guarantors_loan_application_id",
                table: "guarantors",
                column: "loan_application_id");

            migrationBuilder.CreateIndex(
                name: "IX_guarantors_loan_id",
                table: "guarantors",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_client_id",
                table: "loan_applications",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_group_id",
                table: "loan_applications",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_loan_id",
                table: "loan_applications",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_loan_product_id",
                table: "loan_applications",
                column: "loan_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_loan_purpose_id",
                table: "loan_applications",
                column: "loan_purpose_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_office_id",
                table: "loan_applications",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_status",
                table: "loan_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_loan_charges_loan_id",
                table: "loan_charges",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_product_charges_loan_product_id",
                table: "loan_product_charges",
                column: "loan_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_products_name",
                table: "loan_products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayment_schedules_loan_id",
                table: "loan_repayment_schedules",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_reschedule_requests_loan_id",
                table: "loan_reschedule_requests",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transaction_repayment_schedule_mappings_loan_repayment_~",
                table: "loan_transaction_repayment_schedule_mappings",
                column: "loan_repayment_schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transaction_repayment_schedule_mappings_loan_transactio~",
                table: "loan_transaction_repayment_schedule_mappings",
                column: "loan_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_client_id",
                table: "loan_transactions",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_loan_id",
                table: "loan_transactions",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_loan_repayment_schedule_id",
                table: "loan_transactions",
                column: "loan_repayment_schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_payment_detail_id",
                table: "loan_transactions",
                column: "payment_detail_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_transactions_status",
                table: "loan_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_loans_account_number",
                table: "loans",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loans_client_id",
                table: "loans",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_loans_loan_product_id",
                table: "loans",
                column: "loan_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_loans_loan_purpose_id",
                table: "loans",
                column: "loan_purpose_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_type_reference_id",
                table: "notes",
                columns: new[] { "type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "IX_office_transactions_from_office_id",
                table: "office_transactions",
                column: "from_office_id");

            migrationBuilder.CreateIndex(
                name: "IX_office_transactions_to_office_id",
                table: "office_transactions",
                column: "to_office_id");

            migrationBuilder.CreateIndex(
                name: "IX_offices_parent_id",
                table: "offices",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_other_income_other_income_type_id",
                table: "other_income",
                column: "other_income_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_details_payment_type_id",
                table: "payment_details",
                column: "payment_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_payroll_template_id",
                table: "payroll",
                column: "payroll_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_meta_payroll_id",
                table: "payroll_meta",
                column: "payroll_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_meta_payroll_template_meta_id",
                table: "payroll_meta",
                column: "payroll_template_meta_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_template_meta_payroll_template_id",
                table: "payroll_template_meta",
                column: "payroll_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_parent_id",
                table: "permissions",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_persistences_user_id",
                table: "persistences",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "persistences_code_unique",
                table: "persistences",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_scheduler_run_history_report_schedule_id",
                table: "report_scheduler_run_history",
                column: "report_schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_users_role_id",
                table: "role_users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_account_number",
                table: "savings",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_savings_balance",
                table: "savings",
                column: "balance");

            migrationBuilder.CreateIndex(
                name: "IX_savings_client_id",
                table: "savings",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_interest_rate",
                table: "savings",
                column: "interest_rate");

            migrationBuilder.CreateIndex(
                name: "IX_savings_last_interest_posting_date",
                table: "savings",
                column: "last_interest_posting_date");

            migrationBuilder.CreateIndex(
                name: "IX_savings_savings_product_id",
                table: "savings",
                column: "savings_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_charges_savings_id",
                table: "savings_charges",
                column: "savings_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_product_charges_savings_product_id",
                table: "savings_product_charges",
                column: "savings_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_income_fee_id",
                table: "savings_products",
                column: "gl_account_income_fee_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_income_interest_id",
                table: "savings_products",
                column: "gl_account_income_interest_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_income_penalty_id",
                table: "savings_products",
                column: "gl_account_income_penalty_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_interest_on_savings_id",
                table: "savings_products",
                column: "gl_account_interest_on_savings_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_overdraft_portfolio_id",
                table: "savings_products",
                column: "gl_account_overdraft_portfolio_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_savings_control_id",
                table: "savings_products",
                column: "gl_account_savings_control_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_savings_reference_id",
                table: "savings_products",
                column: "gl_account_savings_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_products_gl_account_savings_written_off_id",
                table: "savings_products",
                column: "gl_account_savings_written_off_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_balance",
                table: "savings_transactions",
                column: "balance");

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_payment_detail_id",
                table: "savings_transactions",
                column: "payment_detail_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_savings_id",
                table: "savings_transactions",
                column: "savings_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_savings_id_amount",
                table: "savings_transactions",
                columns: new[] { "savings_id", "amount" });

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_status",
                table: "savings_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_savings_transactions_transaction_type",
                table: "savings_transactions",
                column: "transaction_type");

            migrationBuilder.CreateIndex(
                name: "throttle_user_id_index",
                table: "throttle",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_permission_id",
                table: "user_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_office_id",
                table: "users",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "users_email_unique",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activations");

            migrationBuilder.DropTable(
                name: "asset_depreciation");

            migrationBuilder.DropTable(
                name: "audit_trail");

            migrationBuilder.DropTable(
                name: "charges");

            migrationBuilder.DropTable(
                name: "client_identifications");

            migrationBuilder.DropTable(
                name: "client_next_of_gaur");

            migrationBuilder.DropTable(
                name: "client_next_of_kin");

            migrationBuilder.DropTable(
                name: "client_profession");

            migrationBuilder.DropTable(
                name: "client_users");

            migrationBuilder.DropTable(
                name: "collateral");

            migrationBuilder.DropTable(
                name: "communication_campaigns");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "custom_field_values");

            migrationBuilder.DropTable(
                name: "custom_fields_meta");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "expense_budgets");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "funds");

            migrationBuilder.DropTable(
                name: "gl_journal_entries");

            migrationBuilder.DropTable(
                name: "group_clients");

            migrationBuilder.DropTable(
                name: "group_loan_allocation");

            migrationBuilder.DropTable(
                name: "group_users");

            migrationBuilder.DropTable(
                name: "guarantors");

            migrationBuilder.DropTable(
                name: "loan_charges");

            migrationBuilder.DropTable(
                name: "loan_product_charges");

            migrationBuilder.DropTable(
                name: "loan_provisioning_criteria");

            migrationBuilder.DropTable(
                name: "loan_reschedule_requests");

            migrationBuilder.DropTable(
                name: "loan_transaction_repayment_schedule_mappings");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "office_transactions");

            migrationBuilder.DropTable(
                name: "other_income");

            migrationBuilder.DropTable(
                name: "payment_details");

            migrationBuilder.DropTable(
                name: "payment_type_details");

            migrationBuilder.DropTable(
                name: "payroll_meta");

            migrationBuilder.DropTable(
                name: "persistences");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropTable(
                name: "report_scheduler_run_history");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "role_users");

            migrationBuilder.DropTable(
                name: "savings_charges");

            migrationBuilder.DropTable(
                name: "savings_product_charges");

            migrationBuilder.DropTable(
                name: "savings_transactions");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "sms_gateways");

            migrationBuilder.DropTable(
                name: "throttle");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "client_identification_types");

            migrationBuilder.DropTable(
                name: "client_relationships");

            migrationBuilder.DropTable(
                name: "collateral_types");

            migrationBuilder.DropTable(
                name: "custom_fields");

            migrationBuilder.DropTable(
                name: "expense_types");

            migrationBuilder.DropTable(
                name: "gl_closures");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "loan_applications");

            migrationBuilder.DropTable(
                name: "loan_transactions");

            migrationBuilder.DropTable(
                name: "other_income_types");

            migrationBuilder.DropTable(
                name: "payment_types");

            migrationBuilder.DropTable(
                name: "payroll");

            migrationBuilder.DropTable(
                name: "payroll_template_meta");

            migrationBuilder.DropTable(
                name: "report_scheduler");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "savings");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "asset_types");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "loan_repayment_schedules");

            migrationBuilder.DropTable(
                name: "payroll_templates");

            migrationBuilder.DropTable(
                name: "savings_products");

            migrationBuilder.DropTable(
                name: "offices");

            migrationBuilder.DropTable(
                name: "loans");

            migrationBuilder.DropTable(
                name: "gl_accounts");

            migrationBuilder.DropTable(
                name: "loan_products");

            migrationBuilder.DropTable(
                name: "loan_purposes");
        }
    }
}
