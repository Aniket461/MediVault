using MediVault.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    [DbContext(typeof(MediVaultDbContext))]
    [Migration("20261024160000_OpinionSeenStatus")]
    public class OpinionSeenStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DoctorSeenStatus",
                table: "Opinions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientSeenStatus",
                table: "Opinions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Opinions"
                SET "PatientSeenStatus" = "Status", "DoctorSeenStatus" = "Status"
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DoctorSeenStatus", table: "Opinions");
            migrationBuilder.DropColumn(name: "PatientSeenStatus", table: "Opinions");
        }
    }
}
