using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Domain.Entities;

namespace ILLiasu_Local_Mall.Areas.Admin.ViewModels;

public sealed class ProductEditForm : ProductEditInput
{
    public IReadOnlyList<CategoryOption> Categories { get; set; } = [];
    public IReadOnlyList<UnitOfMeasure> Units { get; set; } = [];
    public Dictionary<Guid, ExistingVariantDisplay> Display { get; set; } = [];
}
public sealed record ExistingVariantDisplay(ProductVariant Variant, VariantStockMapping? Mapping, string OwnerName, string BaseUnitName);
