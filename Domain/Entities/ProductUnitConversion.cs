namespace ILLiasu_Local_Mall.Domain.Entities;

// Factor: 1 FromUnit = Factor canonical ToUnits, for this exact product and SKU only.
// No implicit conversion, reciprocal/chained conversion or link to another SKU.
public sealed class ProductUnitConversion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public Guid FromUnitOfMeasureId { get; set; }
    public UnitOfMeasure FromUnitOfMeasure { get; set; } = null!;
    public Guid ToUnitOfMeasureId { get; set; }
    public UnitOfMeasure ToUnitOfMeasure { get; set; } = null!;
    public decimal Factor { get; set; }
    public bool IsActive { get; set; } = true;
}
