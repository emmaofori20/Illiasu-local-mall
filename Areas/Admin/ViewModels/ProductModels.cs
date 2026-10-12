using ILLiasu_Local_Mall.Application.Catalog;
namespace ILLiasu_Local_Mall.Areas.Admin.ViewModels;

public sealed class ProductStatusForm
{
    public string Snapshot { get; set; } = "";
    public bool Confirm { get; set; }
    public ProductSnapshot Product { get; set; } = new(Guid.Empty, "", true, "", true, 0);
}
public sealed record AdminPagination(int Page, int Pages, int Total, int PageSize, int Count, string Label, string Key, Dictionary<string,string> Filters);
