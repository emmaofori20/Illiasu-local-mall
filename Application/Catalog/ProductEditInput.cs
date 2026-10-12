using System.ComponentModel.DataAnnotations;

namespace ILLiasu_Local_Mall.Application.Catalog;

public class ProductEditInput : ProductCreateInput
{
    public ProductEditInput() { Variants = []; }
    public Guid Id { get; set; }
    [Required] public string Version { get; set; } = "";
    public List<ExistingVariantEdit> ExistingVariants { get; set; } = [];
}

// Only these existing-option fields are writable. Identity, units and mapping fields are absent.
public sealed class ExistingVariantEdit
{
    public Guid Id { get; set; }
    [Required] public string Version { get; set; } = "";
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal Price { get; set; }
    [StringLength(64), RegularExpression("^[!-~]+$", ErrorMessage = "Barcode must use printable ASCII without spaces.")]
    public string? Barcode { get; set; }
    public bool IsActive { get; set; }
    public bool IsAvailable { get; set; }
}
