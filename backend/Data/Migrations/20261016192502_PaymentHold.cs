using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayPalEmail",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalCaptureId",
                table: "Opinions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalPayoutId",
                table: "Opinions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplyBy",
                table: "Opinions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayPalEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayPalCaptureId",
                table: "Opinions");

            migrationBuilder.DropColumn(
                name: "PayPalPayoutId",
                table: "Opinions");

            migrationBuilder.DropColumn(
                name: "ReplyBy",
                table: "Opinions");
        }
    }
}
