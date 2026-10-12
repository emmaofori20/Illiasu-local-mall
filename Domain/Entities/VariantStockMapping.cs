namespace ILLiasu_Local_Mall.Domain.Entities;

// One priced selling SKU consumes UnitsPerSale units from one explicit root SKU.
public sealed class VariantStockMapping
{
    public Guid ProductVariantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid SoldUnitOfMeasureId { get; set; }
    public Guid StockVariantId { get; set; }
    public Guid BaseUnitOfMeasureId { get; set; }
    public decimal UnitsPerSale { get; set; } = 1m;
    public ProductVariant ProductVariant { get; set; } = null!;
    public ProductVariant StockVariant { get; set; } = null!;
}
