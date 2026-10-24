using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MediVault.Api.Data;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    [DbContext(typeof(MediVaultDbContext))]
    [Migration("20261021190000_ClinicalIntake")]
    public class ClinicalIntake : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Intake",
                table: "Opinions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalAid",
                table: "Opinions",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Intake", table: "Opinions");
            migrationBuilder.DropColumn(name: "ClinicalAid", table: "Opinions");
        }
    }
}
