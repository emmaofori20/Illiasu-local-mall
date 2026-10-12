using ILLiasu_Local_Mall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence.Configurations;

public sealed class VariantStockMappingConfiguration : IEntityTypeConfiguration<VariantStockMapping>
{
    public void Configure(EntityTypeBuilder<VariantStockMapping> b)
    {
        b.ToTable("VariantStockMappings", t =>
        {
            t.HasCheckConstraint("CK_VariantStockMappings_PositiveConsumption", "[UnitsPerSale] > 0");
            t.HasCheckConstraint("CK_VariantStockMappings_SelfOwned", "[ProductVariantId] <> [StockVariantId] OR ([UnitsPerSale] = 1 AND [SoldUnitOfMeasureId] = [BaseUnitOfMeasureId])");
        });
        b.HasKey(x => x.ProductVariantId);
        b.Property(x => x.UnitsPerSale).HasPrecision(18, 6);
        b.HasOne(x => x.ProductVariant).WithOne()
            .HasForeignKey<VariantStockMapping>(x => new { x.ProductVariantId, x.ProductId, x.SoldUnitOfMeasureId })
            .HasPrincipalKey<ProductVariant>(x => new { x.Id, x.ProductId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.StockVariant).WithMany()
            .HasForeignKey(x => new { x.StockVariantId, x.ProductId, x.BaseUnitOfMeasureId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.StockVariantId);
    }
}
