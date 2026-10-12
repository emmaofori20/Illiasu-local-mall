namespace ILLiasu_Local_Mall.Application.Catalog;

// Read models only; existing domain entities and their EF mappings remain unchanged.
public sealed record ProductRow(Guid Id, string Name, string Category, bool CategoryActive, int Variants, decimal? MinimumPrice, decimal? MaximumPrice, bool IsActive, string? ImagePath, string? ImageAlt);
public sealed record CategoryOption(Guid Id, string Name, bool IsActive);
public sealed record ProductList(IReadOnlyList<ProductRow> Items, IReadOnlyList<CategoryOption> Categories, string Search, Guid? CategoryId, string Status, int Page, int Pages, int Total, int PageSize, bool Failed = false);
