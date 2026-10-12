using ILLiasu_Local_Mall.Application.Catalog;

namespace ILLiasu_Local_Mall.Areas.Admin.ViewModels;

public sealed class CategoryForm : CategoryDetailsInput
{
    public string Snapshot { get; set; } = "";
    public Guid Id { get; set; }
    public int Products { get; set; }
}
public sealed class CategoryStatusForm
{
    public string Snapshot { get; set; } = "";
    public bool Confirm { get; set; }
    public CategorySnapshot Category { get; set; } = new(Guid.Empty, "", "", true, 0);
}
public sealed record CategoryList(IReadOnlyList<CategorySnapshot> Items, string Search, string Status, int Page, int Pages, int Total, int PageSize = 10);
