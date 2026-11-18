using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCKash.Infrastructure.Data.Migrations
{
    /// <summary>Links a resent login/reset code to the one before it, so a late SMS still works (see LoginOtp.PreviousOtpId).</summary>
    public partial class AddLoginOtpChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "previous_otp_id",
                table: "login_otps",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "previous_otp_id", table: "login_otps");
        }
    }
}
