using System.Text.Json;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Application.Catalog;
public sealed record ProductSnapshot(Guid Id, string Name, bool IsActive, string Version, bool CategoryActive, int Variants);

public sealed class ProductManagement(ApplicationDbContext db, IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("ILLiasu.Products.Status.v1");
    public async Task<ProductList> ListAsync(string? search, Guid? categoryId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        search = (search ?? "").Trim(); if (search.Length > 100) search = search[..100];
        status = status is "active" or "inactive" ? status : "all";
        if (pageSize is not (10 or 25 or 50 or 100)) pageSize = 10;
        var query = db.Products.AsNoTracking();
        if (search.Length > 0) query = query.Where(x => x.Name.Contains(search) || x.Variants.Any(v => v.Sku.Contains(search)));
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (status != "all") query = query.Where(x => x.IsActive == (status == "active"));
        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)); page = Math.Clamp(page, 1, pages);
        // SQL performs filters, aggregates, deterministic paging and thumbnail selection.
        // No Include graphs, per-row queries, tracking or in-memory catalogue filtering.
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProductRow(x.Id, x.Name, x.Category.Name, x.Category.IsActive, x.Variants.Count,
                x.Variants.Select(v => (decimal?)v.Price).Min(), x.Variants.Select(v => (decimal?)v.Price).Max(), x.IsActive,
                x.Images.OrderBy(i => i.ProductVariantId != null).ThenByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => i.Path).FirstOrDefault(),
                x.Images.OrderBy(i => i.ProductVariantId != null).ThenByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => i.AltText).FirstOrDefault()))
            .ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new CategoryOption(x.Id, x.Name, x.IsActive)).ToListAsync(ct);
        return new(rows, categories, search, categoryId, status, page, pages, total, pageSize);
    }
    public async Task<ProductSnapshot?> FindAsync(Guid id, CancellationToken ct)
    {
        var value = await db.Products.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, x.IsActive, x.RowVersion, CategoryActive = x.Category.IsActive, Variants = x.Variants.Count }).SingleOrDefaultAsync(ct);
        return value is null ? null : new(value.Id, value.Name, value.IsActive, Convert.ToBase64String(value.RowVersion), value.CategoryActive, value.Variants);
    }
    public string Sign(ProductSnapshot value) => protector.Protect(JsonSerializer.Serialize(value));
    public ProductSnapshot? Read(string token, Guid id)
    {
        try
        {
            var value = JsonSerializer.Deserialize<ProductSnapshot>(protector.Unprotect(token));
            return value?.Id == id && Convert.FromBase64String(value.Version).Length == 8 ? value : null;
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException or FormatException) { return null; }
    }
    public async Task<bool> ChangeStatusAsync(ProductSnapshot old, CancellationToken ct)
    {
        var version = Convert.FromBase64String(old.Version);
        return await db.Products.Where(x => x.Id == old.Id && x.RowVersion == version && x.IsActive == old.IsActive && x.Category.IsActive == old.CategoryActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, !old.IsActive), ct) == 1;
    }
    // Only local application image paths are rendered; external/unsafe URLs are not fetched.
    public static string? SafeImagePath(string? path) => path is not null && path.StartsWith('/') && !path.StartsWith("//") &&
        !path.Contains('\\') && !path.Contains("..") && !path.Any(char.IsControl) && !path.Contains('%') && !path.Contains('?') && !path.Contains('#') ? path : null;
}
