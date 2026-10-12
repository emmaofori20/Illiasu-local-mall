using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILLiasu_Local_Mall.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_Categories_Slug", "LEN(LTRIM(RTRIM([Slug]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                    table.CheckConstraint("CK_UnitsOfMeasure_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_UnitsOfMeasure_Kind", "[Kind] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_UnitsOfMeasure_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_UnitsOfMeasure_Precision", "[DecimalPlaces] BETWEEN 0 AND 6 AND ([Kind] IN (3,4) OR [DecimalPlaces] = 0)");
                    table.CheckConstraint("CK_UnitsOfMeasure_Symbol", "LEN(LTRIM(RTRIM([Symbol]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_Products_Slug", "LEN(LTRIM(RTRIM([Slug]))) > 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Sku = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Barcode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AS"),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityStep = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AllowFractionalQuantity = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.UniqueConstraint("AK_ProductVariants_Id_ProductId", x => new { x.Id, x.ProductId });
                    table.UniqueConstraint("AK_ProductVariants_Id_ProductId_UnitOfMeasureId", x => new { x.Id, x.ProductId, x.UnitOfMeasureId });
                    table.CheckConstraint("CK_ProductVariants_Barcode", "[Barcode] IS NULL OR (LEN([Barcode]) > 0 AND DATALENGTH([Barcode]) = DATALENGTH(LTRIM(RTRIM([Barcode]))))");
                    table.CheckConstraint("CK_ProductVariants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_ProductVariants_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_ProductVariants_QuantityStep", "[QuantityStep] > 0 AND ([AllowFractionalQuantity] = 1 OR [QuantityStep] = FLOOR([QuantityStep]))");
                    table.CheckConstraint("CK_ProductVariants_Sku", "LEN([Sku]) > 0 AND DATALENGTH([Sku]) = DATALENGTH(LTRIM(RTRIM([Sku])))");
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductVariants_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_AltText", "LEN(LTRIM(RTRIM([AltText]))) > 0");
                    table.CheckConstraint("CK_ProductImages_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_ProductImages_Path", "LEN(LTRIM(RTRIM([Path]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProductImages_ProductVariants_ProductVariantId_ProductId",
                        columns: x => new { x.ProductVariantId, x.ProductId },
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "Id", "ProductId" });
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductUnitConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUnitConversions", x => x.Id);
                    table.CheckConstraint("CK_ProductUnitConversions_DifferentUnits", "[FromUnitOfMeasureId] <> [ToUnitOfMeasureId]");
                    table.CheckConstraint("CK_ProductUnitConversions_Factor", "[Factor] > 0");
                    table.ForeignKey(
                        name: "FK_ProductUnitConversions_ProductVariants_ProductVariantId_ProductId_ToUnitOfMeasureId",
                        columns: x => new { x.ProductVariantId, x.ProductId, x.ToUnitOfMeasureId },
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "Id", "ProductId", "UnitOfMeasureId" });
                    table.ForeignKey(
                        name: "FK_ProductUnitConversions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductUnitConversions_UnitsOfMeasure_FromUnitOfMeasureId",
                        column: x => x.FromUnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductUnitConversions_UnitsOfMeasure_ToUnitOfMeasureId",
                        column: x => x.ToUnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductVariantId_ProductId",
                table: "ProductImages",
                columns: new[] { "ProductVariantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ProductPrimary",
                table: "ProductImages",
                column: "ProductId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [ProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_VariantPrimary",
                table: "ProductImages",
                columns: new[] { "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [ProductVariantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitConversions_FromUnitOfMeasureId",
                table: "ProductUnitConversions",
                column: "FromUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitConversions_ProductId_ProductVariantId_FromUnitOfMeasureId",
                table: "ProductUnitConversions",
                columns: new[] { "ProductId", "ProductVariantId", "FromUnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitConversions_ProductVariantId_ProductId_ToUnitOfMeasureId",
                table: "ProductUnitConversions",
                columns: new[] { "ProductVariantId", "ProductId", "ToUnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitConversions_ToUnitOfMeasureId",
                table: "ProductUnitConversions",
                column: "ToUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Barcode",
                table: "ProductVariants",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Sku",
                table: "ProductVariants",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_UnitOfMeasureId",
                table: "ProductVariants",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "ProductUnitConversions");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
