using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayPalOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayPalOrderId",
                table: "Opinions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayPalOrderId",
                table: "Opinions");
        }
    }
}
