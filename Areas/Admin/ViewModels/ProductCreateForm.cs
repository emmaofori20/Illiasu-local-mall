using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Domain.Entities;
namespace ILLiasu_Local_Mall.Areas.Admin.ViewModels;
public sealed class ProductCreateForm : ProductCreateInput
{
    public List<IFormFile> Images { get; set; } = [];
    public int? PrimaryImageIndex { get; set; }
    public IReadOnlyList<CategoryOption> Categories { get; set; } = [];
    public IReadOnlyList<UnitOfMeasure> Units { get; set; } = [];
}
