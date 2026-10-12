using System.ComponentModel.DataAnnotations;
using System.Data;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Application.Catalog;

public sealed class ProductEditing(ApplicationDbContext db, ProductCreation creation)
{
    public static bool MatchesVersion(string? encoded, byte[] actual)
    {
        try { return Convert.FromBase64String(encoded ?? "").AsSpan().SequenceEqual(actual) && actual.Length == 8; }
        catch (FormatException) { return false; }
    }

    public async Task<CreationResult> EditAsync(ProductEditInput input, CancellationToken ct = default)
    {
        ProductCreation.Normalize(input);
        input.ExistingVariants ??= [];
        foreach (var row in input.ExistingVariants) row.Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode.Trim();
        var errors = new List<CreationError>();
        void Error(string key, string message) => errors.Add(new(key, message));
        void Validate(object value, string prefix)
        {
            var results = new List<ValidationResult>(); Validator.TryValidateObject(value, new ValidationContext(value), results, true);
            foreach (var result in results) foreach (var member in result.MemberNames.DefaultIfEmpty("")) Error(prefix + member, result.ErrorMessage!);
        }
        // Slug is fetched from SQL, never edited or trusted from the request.
        if (input.ExistingVariants.Count > 25 || input.Variants.Count > 25) return new(null, [new("", "Use at most 25 selling options.")]);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var product = await db.Products.Include(p => p.Variants).SingleOrDefaultAsync(p => p.Id == input.Id, ct);
            if (product is null) return new(null, [new("", "This product is no longer available.")]);
            input.Slug = product.Slug;
            Validate(input, "");
            var stored = product.Variants.ToDictionary(v => v.Id);
            if (!MatchesVersion(input.Version, product.RowVersion)) Error("", "This product changed since you opened it. Reload the page and review the latest values; nothing was saved.");
            if (input.ExistingVariants.Count != stored.Count || input.ExistingVariants.Select(v => v.Id).Distinct().Count() != stored.Count || input.ExistingVariants.Any(v => !stored.ContainsKey(v.Id)))
                Error("ExistingVariants", "The selling-option list changed or contains invalid references. Reload the page; existing options cannot be removed.");
            for (var i = 0; i < input.ExistingVariants.Count; i++)
            {
                var row = input.ExistingVariants[i]; Validate(row, $"ExistingVariants[{i}].");
                if (stored.TryGetValue(row.Id, out var variant) && !MatchesVersion(row.Version, variant.RowVersion))
                    Error($"ExistingVariants[{i}].Version", "This selling option changed. Reload and review the latest values; nothing was saved.");
                if (decimal.Round(row.Price, 2) != row.Price) Error($"ExistingVariants[{i}].Price", "Price supports at most two decimal places.");
            }
            if (stored.Count + input.Variants.Count is < 1 or > 25) Error("Variants", "A product must have between one and 25 selling options, including inactive options.");
            var mappings = await db.VariantStockMappings.AsNoTracking().Where(m => m.ProductId == input.Id).ToDictionaryAsync(m => m.ProductVariantId, ct);
            if (stored.Keys.Any(id => !mappings.ContainsKey(id))) Error("", "A stock mapping is missing. Ask an administrator to review the catalogue; nothing was saved.");
            if (errors.Count > 0) return new(null, errors);

            // Build authoritative immutable options from persisted identities and mappings.
            var aggregate = new ProductCreateInput { Name = input.Name, Slug = product.Slug, CategoryId = input.CategoryId, Description = input.Description, IsActive = input.IsActive, Variants = [] };
            foreach (var row in input.ExistingVariants)
            {
                var v = stored[row.Id]; var m = mappings[v.Id];
                aggregate.Variants.Add(new VariantCreateInput { ClientRowKey = v.Id, Name = v.Name, Sku = v.Sku,
                    Barcode = row.Barcode, Price = row.Price, UnitOfMeasureId = v.UnitOfMeasureId, QuantityStep = v.QuantityStep,
                    AllowFractionalQuantity = v.AllowFractionalQuantity, IsActive = row.IsActive, IsAvailable = row.IsAvailable,
                    StockMode = m.StockVariantId == v.Id ? StockBehaviour.Own : StockBehaviour.Shared,
                    StockOwnerRowKey = m.StockVariantId == v.Id ? null : m.StockVariantId, UnitsPerSale = m.UnitsPerSale });
            }
            aggregate.Variants.AddRange(input.Variants);
            var validation = await creation.ValidateCatalogueAsync(aggregate, product.Id, product.CategoryId, stored.Values.Select(v => v.UnitOfMeasureId).ToArray(), ct);
            // Inactive units remain valid only for historical existing rows; new options need active definitions.
            var activeUnits = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
            for (var i = 0; i < input.Variants.Count; i++)
            {
                if (!activeUnits.Contains(input.Variants[i].UnitOfMeasureId)) Error($"Variants[{i}].UnitOfMeasureId", "Choose a currently active selling unit.");
                for (var j = 0; j < input.Variants[i].Conversions.Count; j++)
                    if (!activeUnits.Contains(input.Variants[i].Conversions[j].FromUnitOfMeasureId)) Error($"Variants[{i}].Conversions[{j}].FromUnitOfMeasureId", "Choose a currently active receiving unit.");
            }
            foreach (var error in validation)
            {
                var field = error.Field;
                if (field.StartsWith("Variants[", StringComparison.Ordinal))
                {
                    var end = field.IndexOf(']'); var index = int.Parse(field[9..end]);
                    field = index < stored.Count ? "ExistingVariants[" + index + field[end..] : "Variants[" + (index - stored.Count) + field[end..];
                }
                Error(field, error.Message);
            }
            if (errors.Count > 0) return new(null, errors);

            db.Entry(product).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(input.Version);
            product.Name = input.Name; product.Description = input.Description; product.CategoryId = input.CategoryId; product.IsActive = input.IsActive;
            // Every edit advances the aggregate token, including option-only edits/additions.
            db.Entry(product).Property(p => p.Name).IsModified = true;
            foreach (var row in input.ExistingVariants)
            {
                var v = stored[row.Id]; db.Entry(v).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(row.Version);
                v.Price = row.Price; v.Barcode = row.Barcode; v.IsActive = row.IsActive; v.IsAvailable = row.IsAvailable;
            }
            var owners = new Dictionary<Guid, ProductVariant>(stored);
            foreach (var row in input.Variants)
            {
                var v = new ProductVariant { ProductId = product.Id, Name = row.Name, Sku = row.Sku, Barcode = row.Barcode,
                    UnitOfMeasureId = row.UnitOfMeasureId, Price = row.Price, QuantityStep = row.QuantityStep,
                    AllowFractionalQuantity = row.AllowFractionalQuantity, IsActive = row.IsActive, IsAvailable = row.IsAvailable };
                db.ProductVariants.Add(v); product.Variants.Add(v); owners.Add(row.ClientRowKey, v);
                foreach (var c in row.Conversions) db.ProductUnitConversions.Add(new ProductUnitConversion { ProductId = product.Id, ProductVariantId = v.Id,
                    FromUnitOfMeasureId = c.FromUnitOfMeasureId, ToUnitOfMeasureId = v.UnitOfMeasureId, Factor = c.Factor, IsActive = c.IsActive });
            }
            foreach (var row in input.Variants)
            {
                var v = owners[row.ClientRowKey]; var owner = row.StockMode == StockBehaviour.Own ? v : owners[row.StockOwnerRowKey!.Value];
                db.VariantStockMappings.Add(new VariantStockMapping { ProductVariantId = v.Id, ProductId = product.Id, SoldUnitOfMeasureId = v.UnitOfMeasureId,
                    StockVariantId = owner.Id, BaseUnitOfMeasureId = owner.UnitOfMeasureId, UnitsPerSale = row.StockMode == StockBehaviour.Own ? 1 : row.UnitsPerSale!.Value });
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(product.Id, []);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            return new(null, [new("", "The product changed during saving. Reload and review the latest values; nothing was saved.")]);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException)
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            return new(null, [new("", "A SKU, barcode or catalogue constraint changed during saving. Review the identifiers and retry; nothing was saved.")]);
        }
        catch (SqlException e) when (e.Number == 1205)
        {
            db.ChangeTracker.Clear();
            return new(null, [new("", "A concurrent catalogue update interrupted saving. Review and retry; nothing was saved.")]);
        }
    }
}
