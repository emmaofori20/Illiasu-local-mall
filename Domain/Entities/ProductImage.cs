namespace ILLiasu_Local_Mall.Domain.Entities;

public sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public string Path { get; set; } = "";
    public string AltText { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
