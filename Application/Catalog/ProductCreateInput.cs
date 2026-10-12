using System.ComponentModel.DataAnnotations;
namespace ILLiasu_Local_Mall.Application.Catalog;

public class ProductCreateInput
{
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [Required, StringLength(180), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
    public Guid CategoryId { get; set; }
    [StringLength(4000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<VariantCreateInput> Variants { get; set; } = [new()];
}
public sealed class VariantCreateInput
{
    public Guid ClientRowKey { get; set; } = Guid.NewGuid();
    [EnumDataType(typeof(StockBehaviour))] public StockBehaviour StockMode { get; set; } = StockBehaviour.Own;
    public Guid? StockOwnerRowKey { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal? UnitsPerSale { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(64), RegularExpression("^[!-~]+$", ErrorMessage = "SKU must use printable ASCII without spaces.")] public string Sku { get; set; } = "";
    [StringLength(64), RegularExpression("^[!-~]+$", ErrorMessage = "Barcode must use printable ASCII without spaces.")] public string? Barcode { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal Price { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal QuantityStep { get; set; } = 1;
    public bool AllowFractionalQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public List<ConversionCreateInput> Conversions { get; set; } = [];
}
public enum StockBehaviour { Own = 0, Shared = 1 }
public sealed class ConversionCreateInput
{
    public Guid FromUnitOfMeasureId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Factor { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
public sealed record CreationError(string Field, string Message);
public sealed record CreationResult(Guid? Id, IReadOnlyList<CreationError> Errors);
