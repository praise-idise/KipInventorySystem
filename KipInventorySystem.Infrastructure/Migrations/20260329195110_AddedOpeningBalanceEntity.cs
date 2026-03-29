using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KipInventorySystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedOpeningBalanceEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                columns: table => new
                {
                    OpeningBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningBalanceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.OpeningBalanceId);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalanceLines",
                columns: table => new
                {
                    OpeningBalanceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalanceLines", x => x.OpeningBalanceLineId);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                        column: x => x.OpeningBalanceId,
                        principalTable: "OpeningBalances",
                        principalColumn: "OpeningBalanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OpeningBalanceId_ProductId",
                table: "OpeningBalanceLines",
                columns: new[] { "OpeningBalanceId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_ProductId",
                table: "OpeningBalanceLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_OpeningBalanceNumber",
                table: "OpeningBalances",
                column: "OpeningBalanceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_WarehouseId",
                table: "OpeningBalances",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningBalanceLines");

            migrationBuilder.DropTable(
                name: "OpeningBalances");
        }
    }
}
