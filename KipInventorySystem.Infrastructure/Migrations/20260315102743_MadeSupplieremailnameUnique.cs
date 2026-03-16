using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KipInventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MadeSupplieremailnameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Email",
                table: "Suppliers",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Email",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers");
        }
    }
}
