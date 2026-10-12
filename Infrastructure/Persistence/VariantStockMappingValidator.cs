using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Domain;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

// SaveChanges boundary used by every tracked application writer, including imports.
internal static class VariantStockMappingValidator
{
    internal static bool Prepare(ApplicationDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries<VariantStockMapping>().ToList();
        if (entries.Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock mappings are immutable. A reviewed revision workflow is required to change or delete them.");
        if (db.ChangeTracker.Entries<ProductVariant>().Any(e => e.State == EntityState.Modified &&
            (e.Property(v => v.QuantityStep).IsModified || e.Property(v => v.AllowFractionalQuantity).IsModified)))
            throw new InvalidOperationException("Mapped quantity rules require a reviewed revision workflow; price and availability changes remain independent.");
        if (db.ChangeTracker.Entries<UnitOfMeasure>().Any(e => e.State == EntityState.Modified &&
            (e.Property(u => u.DecimalPlaces).IsModified || e.Property(u => u.Kind).IsModified)))
            throw new InvalidOperationException("Unit precision and dimensions require a reviewed revision workflow.");
        var mapped = entries.Where(e => e.State != EntityState.Detached).Select(e => e.Entity.ProductVariantId).ToHashSet();
        // Preserve the current creation workflow without adding form fields or inferring sharing.
        foreach (var variant in db.ChangeTracker.Entries<ProductVariant>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList())
            if (mapped.Add(variant.Id)) db.VariantStockMappings.Add(new VariantStockMapping
            {
                ProductVariantId = variant.Id, ProductId = variant.ProductId,
                SoldUnitOfMeasureId = variant.UnitOfMeasureId, StockVariantId = variant.Id,
                BaseUnitOfMeasureId = variant.UnitOfMeasureId, UnitsPerSale = 1
            });
        return db.ChangeTracker.Entries<VariantStockMapping>().Any(e => e.State == EntityState.Added);
    }

    internal static async Task ValidateAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var pending = db.ChangeTracker.Entries<VariantStockMapping>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        var variantIds = pending.SelectMany(m => new[] { m.ProductVariantId, m.StockVariantId }).Distinct().ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        foreach (var e in db.ChangeTracker.Entries<ProductVariant>().Where(e => e.State is EntityState.Added or EntityState.Modified)) variants[e.Entity.Id] = e.Entity;
        foreach (var e in db.ChangeTracker.Entries<ProductVariant>().Where(e => e.State == EntityState.Deleted)) variants.Remove(e.Entity.Id);
        var roots = await db.VariantStockMappings.AsNoTracking().Where(m => variantIds.Contains(m.ProductVariantId)).ToDictionaryAsync(m => m.ProductVariantId, ct);
        foreach (var mapping in pending)
            if (!roots.TryAdd(mapping.ProductVariantId, mapping)) throw new InvalidOperationException("A selling option already has a stock mapping.");
        var unitIds = pending.Select(m => m.BaseUnitOfMeasureId).Distinct().ToArray();
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        foreach (var e in db.ChangeTracker.Entries<UnitOfMeasure>().Where(e => e.State is EntityState.Added or EntityState.Modified)) units[e.Entity.Id] = e.Entity;
        foreach (var m in pending)
        {
            if (!variants.TryGetValue(m.ProductVariantId, out var sold) || !variants.TryGetValue(m.StockVariantId, out var owner) ||
                sold.ProductId != m.ProductId || owner.ProductId != m.ProductId || sold.UnitOfMeasureId != m.SoldUnitOfMeasureId || owner.UnitOfMeasureId != m.BaseUnitOfMeasureId)
                throw new InvalidOperationException("Selling option and stock owner must belong to the same product and match their declared units.");
            if (!roots.TryGetValue(m.StockVariantId, out var root) || root.StockVariantId != root.ProductVariantId || root.UnitsPerSale != 1 || root.BaseUnitOfMeasureId != root.SoldUnitOfMeasureId)
                throw new InvalidOperationException("Shared stock must point directly to a self-owned root. Cycles, chains and missing roots are not allowed.");
            if (m.ProductVariantId == m.StockVariantId && (m.UnitsPerSale != 1 || m.SoldUnitOfMeasureId != m.BaseUnitOfMeasureId))
                throw new InvalidOperationException("A separately counted option consumes exactly one of its own units.");
            if (!units.TryGetValue(m.BaseUnitOfMeasureId, out var unit)) throw new InvalidOperationException("Choose an existing stock unit.");
            var error = StockConsumptionRules.Validate(m.UnitsPerSale, sold.QuantityStep, unit.DecimalPlaces, owner.AllowFractionalQuantity);
            if (error != null) throw new InvalidOperationException(error);
        }
    }
}
