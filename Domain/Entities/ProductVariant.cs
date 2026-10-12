namespace ILLiasu_Local_Mall.Domain.Entities;

// Each variant is a distinct selling SKU. Stock sharing requires an explicit VariantStockMapping.
public sealed class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; } = null!;
    // GHS price per sold unit; independent of any stock consumption factor.
    public decimal Price { get; set; }
    public decimal QuantityStep { get; set; } = 1m;
    public bool AllowFractionalQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ProductUnitConversion> UnitConversions { get; set; } = new List<ProductUnitConversion>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
