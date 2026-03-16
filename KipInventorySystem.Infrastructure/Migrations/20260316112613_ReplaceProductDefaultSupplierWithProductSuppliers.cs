using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KipInventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceProductDefaultSupplierWithProductSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductSuppliers",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSuppliers", x => new { x.ProductId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_ProductId",
                table: "ProductSuppliers",
                column: "ProductId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_SupplierId",
                table: "ProductSuppliers",
                column: "SupplierId");

            migrationBuilder.Sql("""
                INSERT INTO "ProductSuppliers" ("ProductId", "SupplierId", "IsDefault")
                SELECT "ProductId", "DefaultSupplierId", TRUE
                FROM "Products"
                WHERE "DefaultSupplierId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Suppliers_DefaultSupplierId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_DefaultSupplierId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DefaultSupplierId",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultSupplierId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Products" AS p
                SET "DefaultSupplierId" = ps."SupplierId"
                FROM "ProductSuppliers" AS ps
                WHERE p."ProductId" = ps."ProductId"
                  AND ps."IsDefault" = TRUE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_DefaultSupplierId",
                table: "Products",
                column: "DefaultSupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Suppliers_DefaultSupplierId",
                table: "Products",
                column: "DefaultSupplierId",
                principalTable: "Suppliers",
                principalColumn: "SupplierId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "ProductSuppliers");
        }
    }
}
