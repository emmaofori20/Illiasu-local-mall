using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILLiasu_Local_Mall.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantStockMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VariantStockMappings",
                columns: table => new
                {
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoldUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitsPerSale = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantStockMappings", x => x.ProductVariantId);
                    table.CheckConstraint("CK_VariantStockMappings_PositiveConsumption", "[UnitsPerSale] > 0");
                    table.CheckConstraint("CK_VariantStockMappings_SelfOwned", "[ProductVariantId] <> [StockVariantId] OR ([UnitsPerSale] = 1 AND [SoldUnitOfMeasureId] = [BaseUnitOfMeasureId])");
                    table.ForeignKey(
                        name: "FK_VariantStockMappings_ProductVariants_ProductVariantId_ProductId_SoldUnitOfMeasureId",
                        columns: x => new { x.ProductVariantId, x.ProductId, x.SoldUnitOfMeasureId },
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "Id", "ProductId", "UnitOfMeasureId" });
                    table.ForeignKey(
                        name: "FK_VariantStockMappings_ProductVariants_StockVariantId_ProductId_BaseUnitOfMeasureId",
                        columns: x => new { x.StockVariantId, x.ProductId, x.BaseUnitOfMeasureId },
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "Id", "ProductId", "UnitOfMeasureId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_VariantStockMappings_ProductVariantId_ProductId_SoldUnitOfMeasureId",
                table: "VariantStockMappings",
                columns: new[] { "ProductVariantId", "ProductId", "SoldUnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantStockMappings_StockVariantId",
                table: "VariantStockMappings",
                column: "StockVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantStockMappings_StockVariantId_ProductId_BaseUnitOfMeasureId",
                table: "VariantStockMappings",
                columns: new[] { "StockVariantId", "ProductId", "BaseUnitOfMeasureId" });

            // Preserve every existing SKU as separately counted; infer no sharing.
            migrationBuilder.Sql(@"INSERT INTO [dbo].[VariantStockMappings]
                ([ProductVariantId], [ProductId], [SoldUnitOfMeasureId], [StockVariantId], [BaseUnitOfMeasureId], [UnitsPerSale])
                SELECT [Id], [ProductId], [UnitOfMeasureId], [Id], [UnitOfMeasureId], CAST(1 AS decimal(18,6))
                FROM [dbo].[ProductVariants];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VariantStockMappings");
        }
    }
}
