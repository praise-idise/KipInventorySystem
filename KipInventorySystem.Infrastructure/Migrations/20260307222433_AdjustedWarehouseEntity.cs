using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KipInventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustedWarehouseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "Warehouses");
        }
    }
}
