using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MediVault.Api.Data;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    [DbContext(typeof(MediVaultDbContext))]
    [Migration("20261017163000_OpinionRefundCause")]
    public class OpinionRefundCause : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundCause",
                table: "Opinions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundCause",
                table: "Opinions");
        }
    }
}
