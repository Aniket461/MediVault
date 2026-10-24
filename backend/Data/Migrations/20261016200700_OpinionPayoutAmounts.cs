using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpinionPayoutAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DoctorAmount",
                table: "Opinions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformAmount",
                table: "Opinions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Opinions"
                SET "PlatformAmount" = ROUND("Fee" * 0.05, 2),
                    "DoctorAmount" = "Fee" - ROUND("Fee" * 0.05, 2)
                WHERE "PayPalPayoutId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoctorAmount",
                table: "Opinions");

            migrationBuilder.DropColumn(
                name: "PlatformAmount",
                table: "Opinions");
        }
    }
}
