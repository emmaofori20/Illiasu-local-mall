using ILLiasu_Local_Mall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories", t =>
        {
            t.HasCheckConstraint("CK_Categories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_Categories_Slug", "LEN(LTRIM(RTRIM([Slug]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_Products_Slug", "LEN(LTRIM(RTRIM([Slug]))) > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(180).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasOne(x => x.Category).WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> b)
    {
        b.ToTable("UnitsOfMeasure", t =>
        {
            t.HasCheckConstraint("CK_UnitsOfMeasure_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
            t.HasCheckConstraint("CK_UnitsOfMeasure_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_UnitsOfMeasure_Symbol", "LEN(LTRIM(RTRIM([Symbol]))) > 0");
            t.HasCheckConstraint("CK_UnitsOfMeasure_Kind", "[Kind] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_UnitsOfMeasure_Precision", "[DecimalPlaces] BETWEEN 0 AND 6 AND ([Kind] IN (3,4) OR [DecimalPlaces] = 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(32).IsUnicode(false)
            .UseCollation("Latin1_General_100_CI_AS").IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Symbol).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.ToTable("ProductVariants", t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_ProductVariants_Sku", "LEN([Sku]) > 0 AND DATALENGTH([Sku]) = DATALENGTH(LTRIM(RTRIM([Sku])))");
            t.HasCheckConstraint("CK_ProductVariants_Barcode", "[Barcode] IS NULL OR (LEN([Barcode]) > 0 AND DATALENGTH([Barcode]) = DATALENGTH(LTRIM(RTRIM([Barcode]))))");
            t.HasCheckConstraint("CK_ProductVariants_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_QuantityStep", "[QuantityStep] > 0 AND ([AllowFractionalQuantity] = 1 OR [QuantityStep] = FLOOR([QuantityStep]))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(64).IsUnicode(false)
            .UseCollation("Latin1_General_100_CI_AS").IsRequired();
        b.Property(x => x.Barcode).HasMaxLength(64).IsUnicode(false)
            .UseCollation("Latin1_General_100_CI_AS");
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.QuantityStep).HasPrecision(18, 6);
        b.Property(x => x.RowVersion).IsRowVersion();
        // Uniqueness includes inactive variants: identifiers are not silently reused.
        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        b.HasAlternateKey(x => new { x.Id, x.ProductId });
        b.HasAlternateKey(x => new { x.Id, x.ProductId, x.UnitOfMeasureId });
        b.HasOne(x => x.Product).WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.UnitOfMeasure).WithMany(x => x.Variants)
            .HasForeignKey(x => x.UnitOfMeasureId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProductUnitConversionConfiguration : IEntityTypeConfiguration<ProductUnitConversion>
{
    public void Configure(EntityTypeBuilder<ProductUnitConversion> b)
    {
        b.ToTable("ProductUnitConversions", t =>
        {
            t.HasCheckConstraint("CK_ProductUnitConversions_Factor", "[Factor] > 0");
            t.HasCheckConstraint("CK_ProductUnitConversions_DifferentUnits", "[FromUnitOfMeasureId] <> [ToUnitOfMeasureId]");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Factor).HasPrecision(18, 6);
        b.HasIndex(x => new { x.ProductId, x.ProductVariantId, x.FromUnitOfMeasureId }).IsUnique();
        b.HasOne(x => x.Product).WithMany(x => x.UnitConversions)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        // One directed step to this variant's canonical unit. Enforces product ownership
        // and canonical target at the database level; another SKU cannot be a target.
        b.HasOne(x => x.ProductVariant).WithMany(x => x.UnitConversions)
            .HasForeignKey(x => new { x.ProductVariantId, x.ProductId, x.ToUnitOfMeasureId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.FromUnitOfMeasure).WithMany()
            .HasForeignKey(x => x.FromUnitOfMeasureId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ToUnitOfMeasure).WithMany()
            .HasForeignKey(x => x.ToUnitOfMeasureId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.ToTable("ProductImages", t =>
        {
            t.HasCheckConstraint("CK_ProductImages_Path", "LEN(LTRIM(RTRIM([Path]))) > 0");
            t.HasCheckConstraint("CK_ProductImages_AltText", "LEN(LTRIM(RTRIM([AltText]))) > 0");
            t.HasCheckConstraint("CK_ProductImages_DisplayOrder", "[DisplayOrder] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Path).HasMaxLength(1024).IsRequired();
        b.Property(x => x.AltText).HasMaxLength(256).IsRequired();
        b.HasOne(x => x.Product).WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ProductVariant).WithMany(x => x.Images)
            .HasForeignKey(x => new { x.ProductVariantId, x.ProductId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId })
            .IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.ProductId).IsUnique()
            .HasDatabaseName("UX_ProductImages_ProductPrimary")
            .HasFilter("[IsPrimary] = 1 AND [ProductVariantId] IS NULL");
        b.HasIndex(x => new { x.ProductId, x.ProductVariantId }).IsUnique()
            .HasDatabaseName("UX_ProductImages_VariantPrimary")
            .HasFilter("[IsPrimary] = 1 AND [ProductVariantId] IS NOT NULL");
    }
}
