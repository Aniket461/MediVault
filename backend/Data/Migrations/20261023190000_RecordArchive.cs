using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MediVault.Api.Data;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    [DbContext(typeof(MediVaultDbContext))]
    [Migration("20261023190000_RecordArchive")]
    public class RecordArchive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Records",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ArchivedAt", table: "Records");
        }
    }
}
