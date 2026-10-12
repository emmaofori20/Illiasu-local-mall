using System.ComponentModel.DataAnnotations;
using System.Data;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Domain;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace ILLiasu_Local_Mall.Application.Catalog;

public sealed class ProductCreation(ApplicationDbContext db)
{
    public static void Normalize(ProductCreateInput input)
    {
        input.Name = (input.Name ?? "").Trim(); input.Slug = (input.Slug ?? "").Trim().ToLowerInvariant();
        input.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        input.Variants ??= [];
        foreach (var v in input.Variants) { v.Name = (v.Name ?? "").Trim(); v.Sku = (v.Sku ?? "").Trim(); v.Barcode = string.IsNullOrWhiteSpace(v.Barcode) ? null : v.Barcode.Trim(); v.Conversions ??= []; }
    }
    public async Task<CreationResult> CreateAsync(ProductCreateInput input, CancellationToken ct = default)
    {
        Normalize(input);
        var errors = new List<CreationError>();
        void Error(string field, string message) => errors.Add(new(field,message));
        void Validate(object value, string prefix)
        {
            var results = new List<ValidationResult>(); Validator.TryValidateObject(value,new ValidationContext(value),results,true);
            foreach(var result in results) foreach(var member in result.MemberNames.DefaultIfEmpty("")) Error(prefix+member,result.ErrorMessage!);
        }
        Validate(input, "");
        if(input.Variants.Count is < 1 or > 25) Error("Variants","Add between one and 25 selling options.");
        if(errors.Count > 0) return new(null,errors);
        // Serializable keeps category/UOM eligibility and identifier checks valid until commit.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            errors.AddRange(await ValidateCatalogueAsync(input, ct: ct));
            if(errors.Count>0) { await transaction.RollbackAsync(ct);return new(null,errors); }
            var product=new Product { Name=input.Name,Slug=input.Slug,Description=input.Description,CategoryId=input.CategoryId,IsActive=input.IsActive };
            var created = new Dictionary<Guid,ProductVariant>();
            foreach(var v in input.Variants)
            {
                var variant=new ProductVariant { ProductId=product.Id,Name=v.Name,Sku=v.Sku,Barcode=v.Barcode,UnitOfMeasureId=v.UnitOfMeasureId,Price=v.Price,QuantityStep=v.QuantityStep,AllowFractionalQuantity=v.AllowFractionalQuantity,IsActive=v.IsActive,IsAvailable=v.IsAvailable };
                product.Variants.Add(variant);
                created.Add(v.ClientRowKey,variant);
                foreach(var c in v.Conversions) product.UnitConversions.Add(new ProductUnitConversion { ProductId=product.Id,ProductVariantId=variant.Id,FromUnitOfMeasureId=c.FromUnitOfMeasureId,ToUnitOfMeasureId=v.UnitOfMeasureId,Factor=c.Factor,IsActive=c.IsActive });
            }
            db.Products.Add(product);
            foreach (var v in input.Variants)
            {
                var variant = created[v.ClientRowKey];
                var owner = v.StockMode == StockBehaviour.Own ? variant : created[v.StockOwnerRowKey!.Value];
                db.VariantStockMappings.Add(new VariantStockMapping { ProductVariantId = variant.Id, ProductId = product.Id,
                    SoldUnitOfMeasureId = variant.UnitOfMeasureId, StockVariantId = owner.Id, BaseUnitOfMeasureId = owner.UnitOfMeasureId,
                    UnitsPerSale = v.StockMode == StockBehaviour.Own ? 1m : v.UnitsPerSale!.Value });
            }
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return new(product.Id,[]);
        }
        catch(DbUpdateException e) when(e.InnerException is SqlException)
        {
            await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();
            var sql=(SqlException)e.InnerException!;
            return new(null,[new("",sql.Number is 2601 or 2627 ? "A slug, SKU or barcode was just taken. Review the identifiers and retry; nothing was saved." : "A category, unit or catalogue constraint changed. Review the form and retry; nothing was saved.")]);
        }
        catch(SqlException e) when(e.Number==1205)
        {
            db.ChangeTracker.Clear();return new(null,[new("","A concurrent catalogue update interrupted this save. Retry after reviewing the form; nothing was saved.")]);
        }
    }
    internal async Task<List<CreationError>> ValidateCatalogueAsync(ProductCreateInput input,
        Guid? existingProductId = null, Guid? retainedCategoryId = null, Guid[]? retainedUnits = null, CancellationToken ct = default)
    {
        var retainedUnitIds = retainedUnits ?? [];
        var errors = new List<CreationError>();
        void Error(string field, string message) => errors.Add(new(field,message));
        void Validate(object value, string prefix)
        {
            var results = new List<ValidationResult>(); Validator.TryValidateObject(value,new ValidationContext(value),results,true);
            foreach(var result in results) foreach(var member in result.MemberNames.DefaultIfEmpty("")) Error(prefix+member,result.ErrorMessage!);
        }
            if(!await db.Categories.AnyAsync(x=>x.Id==input.CategoryId && (x.IsActive || x.Id==retainedCategoryId),ct)) Error("CategoryId","Choose a currently active category.");
            if(await db.Products.AnyAsync(x=>x.Slug==input.Slug && x.Id!=existingProductId,ct)) Error("Slug","This product slug is already used.");
            var units = await db.UnitsOfMeasure.AsNoTracking().Where(x=>x.IsActive || retainedUnitIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
            var skus = input.Variants.Select(x=>x.Sku).ToArray(); var barcodes=input.Variants.Where(x=>x.Barcode!=null).Select(x=>x.Barcode!).ToArray();
            var existingSkus = await db.ProductVariants.AsNoTracking().Where(x=>x.ProductId!=existingProductId && skus.Contains(x.Sku)).Select(x=>x.Sku).ToListAsync(ct);
            var existingBarcodes = await db.ProductVariants.AsNoTracking().Where(x=>x.ProductId!=existingProductId && x.Barcode!=null && barcodes.Contains(x.Barcode)).Select(x=>x.Barcode!).ToListAsync(ct);
            var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var seenBarcodes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = input.Variants.GroupBy(v => v.ClientRowKey).Where(g => g.Key != Guid.Empty && g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
            for(var i=0;i<input.Variants.Count;i++)
            {
                var v=input.Variants[i]; var key=$"Variants[{i}]."; Validate(v,key);
                if(v.ClientRowKey == Guid.Empty || !rows.ContainsKey(v.ClientRowKey)) Error(key+"ClientRowKey","Each selling option needs a unique temporary identifier. Reload the form if necessary.");
                if(!seenSkus.Add(v.Sku) || existingSkus.Contains(v.Sku,StringComparer.OrdinalIgnoreCase)) Error(key+"Sku","Product code (SKU) is already used, including inactive selling options.");
                if(v.Barcode!=null && (!seenBarcodes.Add(v.Barcode) || existingBarcodes.Contains(v.Barcode,StringComparer.OrdinalIgnoreCase))) Error(key+"Barcode","Barcode is already used, including inactive selling options.");
                if(decimal.Round(v.Price,2)!=v.Price) Error(key+"Price","Price supports at most two decimal places.");
                units.TryGetValue(v.UnitOfMeasureId,out var unit);
                if(unit is null) Error(key+"UnitOfMeasureId","Choose a currently active selling unit.");
                if(decimal.Round(v.QuantityStep,6)!=v.QuantityStep) Error(key+"QuantityStep","Quantity step supports at most six decimal places.");
                if(!v.AllowFractionalQuantity && decimal.Truncate(v.QuantityStep)!=v.QuantityStep) Error(key+"QuantityStep","Whole-item options require a whole-number selling increment.");
                if(unit!=null)
                {
                    if(v.AllowFractionalQuantity && unit.Kind is not (UnitOfMeasureKind.Mass or UnitOfMeasureKind.Volume)) Error(key+"AllowFractionalQuantity","Fractional quantities require a measured mass or volume unit.");
                    if(decimal.Round(v.QuantityStep,unit.DecimalPlaces)!=v.QuantityStep) Error(key+"QuantityStep","Quantity step exceeds this unit's declared precision.");
                }
                VariantCreateInput? owner = v.StockMode == StockBehaviour.Own ? v : null;
                if (v.StockMode == StockBehaviour.Shared)
                {
                    if (!v.StockOwnerRowKey.HasValue || !rows.TryGetValue(v.StockOwnerRowKey.Value, out owner) || owner == v)
                    { Error(key+"StockOwnerRowKey", "Choose another separately counted option from this product."); owner = null; }
                    else if (owner.StockMode != StockBehaviour.Own)
                    { Error(key+"StockOwnerRowKey", "Choose a separately counted source. Options cannot share through another shared option or form a cycle."); owner = null; }
                    if (!v.UnitsPerSale.HasValue) Error(key+"UnitsPerSale", "Enter how much source stock one sale consumes. No pack quantity is assumed.");
                }
                if (owner != null && units.TryGetValue(owner.UnitOfMeasureId, out var baseUnit))
                {
                    var factor = v.StockMode == StockBehaviour.Own ? 1m : v.UnitsPerSale;
                    if (factor.HasValue)
                    {
                        var stockError = StockConsumptionRules.Validate(factor.Value, v.QuantityStep, baseUnit.DecimalPlaces, owner.AllowFractionalQuantity);
                        if (stockError != null) Error(key + (v.StockMode == StockBehaviour.Own ? "QuantityStep" : "UnitsPerSale"), stockError);
                        for (var j = 0; j < Math.Min(v.Conversions.Count, 10); j++)
                            if (StockConsumptionRules.Validate(factor.Value, v.Conversions[j].Factor, baseUnit.DecimalPlaces, owner.AllowFractionalQuantity) is { } conversionError)
                                Error(key+$"Conversions[{j}].Factor", conversionError);
                    }
                }
                if(v.Conversions.Count>10) Error(key+"Conversions","Use at most ten receiving units per selling option.");
                var sources=new HashSet<Guid>();
                for(var j=0;j<Math.Min(v.Conversions.Count,10);j++)
                {
                    var c=v.Conversions[j];var ck=key+$"Conversions[{j}].";Validate(c,ck);
                    if(!units.ContainsKey(c.FromUnitOfMeasureId)) Error(ck+"FromUnitOfMeasureId","Choose a currently active alternate unit.");
                    if(c.FromUnitOfMeasureId==v.UnitOfMeasureId || !sources.Add(c.FromUnitOfMeasureId)) Error(ck+"FromUnitOfMeasureId","Choose a different receiving unit, used only once for this option.");
                    if(decimal.Round(c.Factor,6)!=c.Factor) Error(ck+"Factor","The received quantity supports at most six decimal places.");
                    if(unit!=null && (decimal.Round(c.Factor,unit.DecimalPlaces)!=c.Factor || !v.AllowFractionalQuantity && decimal.Truncate(c.Factor)!=c.Factor)) Error(ck+"Factor","Enter a valid quantity in this option's selling unit.");
                }
            }
        return errors;
    }

}
