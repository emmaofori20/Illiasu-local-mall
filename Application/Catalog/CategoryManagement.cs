using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ILLiasu_Local_Mall.Application.Catalog;

public sealed record CategorySnapshot(Guid Id, string Name, string Slug, bool IsActive, int Products);

// Signed snapshots and conditional SQL updates prevent stale forms overwriting newer edits.
public sealed class CategoryManagement(ApplicationDbContext db, IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("ILLiasu.Categories.v1");
    private IQueryable<Category> ReadQuery => db.Categories.AsNoTracking();
    public async Task<CategoryPage> ListAsync(string? search, string? status = "all", int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        search = (search ?? "").Trim(); if (search.Length > 100) search = search[..100];
        status = status is "active" or "inactive" ? status : "all";
        if (pageSize is not (10 or 25 or 50 or 100)) pageSize = 10;
        var query = ReadQuery;
        if (search.Length > 0) query = query.Where(x => x.Name.Contains(search) || x.Slug.Contains(search));
        if (status != "all") query = query.Where(x => x.IsActive == (status == "active"));
        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CategorySnapshot(x.Id, x.Name, x.Slug, x.IsActive, x.Products.Count)).ToListAsync(ct);
        return new(items, search, status, page, pages, total, pageSize);
    }
    public Task<CategorySnapshot?> FindAsync(Guid id, CancellationToken ct = default) => ReadQuery.Where(x => x.Id == id)
        .Select(x => new CategorySnapshot(x.Id, x.Name, x.Slug, x.IsActive, x.Products.Count)).SingleOrDefaultAsync(ct);
    public string Sign(CategorySnapshot value) => protector.Protect(JsonSerializer.Serialize(value));
    public CategorySnapshot? Read(string token, Guid id)
    {
        try { var value = JsonSerializer.Deserialize<CategorySnapshot>(protector.Unprotect(token)); return value?.Id == id ? value : null; }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException) { return null; }
    }
    private static CategoryDetailsInput Normalize(string? name, string? slug) => new()
    {
        Name = (name ?? "").Trim(), Slug = (slug ?? "").Trim().ToLowerInvariant()
    };
    private async Task<List<CategoryValidationError>> ValidateAsync(CategoryDetailsInput input, Guid? except, CancellationToken ct)
    {
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), validation, true);
        var errors = validation.SelectMany(v => v.MemberNames.DefaultIfEmpty("")
            .Select(field => new CategoryValidationError(field, v.ErrorMessage!))).ToList();
        if (errors.Count == 0 && await ReadQuery.AnyAsync(x => x.Slug == input.Slug && x.Id != except, ct))
            errors.Add(new("Slug", "This slug is already used, including by inactive categories."));
        return errors;
    }
    private static CategoryWriteResult Result(CategoryWriteOutcome outcome, CategoryDetailsInput input,
        IReadOnlyList<CategoryValidationError>? errors = null, CategorySnapshot? current = null) =>
        new(outcome, input.Name, input.Slug, errors ?? [], current);
    private static bool Unique(Exception e) => e is SqlException { Number: 2601 or 2627 } ||
        e is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };

    public async Task<CategoryWriteResult> CreateAsync(string? name, string? slug, CancellationToken ct = default)
    {
        var input = Normalize(name, slug);
        var errors = await ValidateAsync(input, null, ct);
        if (errors.Count > 0) return Result(CategoryWriteOutcome.ValidationFailed, input, errors);
        var category = new Category { Name = input.Name, Slug = input.Slug, IsActive = true };
        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(ct);
            return Result(CategoryWriteOutcome.Succeeded, input, current: new(category.Id, category.Name, category.Slug, true, 0));
        }
        catch (Exception e) when (Unique(e))
        {
            db.Entry(category).State = EntityState.Detached;
            return Result(CategoryWriteOutcome.ValidationFailed, input, [new("Slug", "This slug was just taken. Choose another.")]);
        }
    }
    private IQueryable<Category> Unchanged(CategorySnapshot old) => db.Categories.Where(x => x.Id == old.Id &&
        EF.Functions.Collate(x.Name, "Latin1_General_100_BIN2") == old.Name &&
        EF.Functions.Collate(x.Slug, "Latin1_General_100_BIN2") == old.Slug && x.IsActive == old.IsActive);
    public async Task<CategoryWriteResult> EditAsync(CategorySnapshot old, string? name, string? slug, CancellationToken ct = default)
    {
        var input = Normalize(name, slug);
        var current = await FindAsync(old.Id, ct);
        if (current is null) return Result(CategoryWriteOutcome.NotFound, input);
        var errors = await ValidateAsync(input, old.Id, ct);
        if (errors.Count > 0) return Result(CategoryWriteOutcome.ValidationFailed, input, errors, current);
        try
        {
            if (await Unchanged(old).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, input.Name).SetProperty(x => x.Slug, input.Slug), ct) == 1)
                return Result(CategoryWriteOutcome.Succeeded, input, current: current with { Name = input.Name, Slug = input.Slug });
        }
        catch (Exception e) when (Unique(e))
        {
            return Result(CategoryWriteOutcome.ValidationFailed, input, [new("Slug", "This slug was just taken. Choose another.")], current);
        }
        current = await FindAsync(old.Id, ct);
        return Result(current is null ? CategoryWriteOutcome.NotFound : CategoryWriteOutcome.Conflict, input, current: current);
    }
    public async Task<CategoryWriteResult> ChangeStatusAsync(CategorySnapshot old, CancellationToken ct = default)
    {
        var input = Normalize(old.Name, old.Slug);
        var current = await FindAsync(old.Id, ct);
        if (current is null) return Result(CategoryWriteOutcome.NotFound, input);
        // Preserve the existing replay rule and reviewed name/slug/product-count conditions.
        if (current.IsActive == !old.IsActive && current.Name == old.Name && current.Slug == old.Slug && current.Products == old.Products)
            return Result(CategoryWriteOutcome.AlreadyApplied, input, current: current);
        if (await Unchanged(old).Where(x => x.Products.Count == old.Products)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, !old.IsActive), ct) == 1)
            return Result(CategoryWriteOutcome.Succeeded, input, current: current with { IsActive = !old.IsActive });
        current = await FindAsync(old.Id, ct);
        return Result(current is null ? CategoryWriteOutcome.NotFound : CategoryWriteOutcome.Conflict, input, current: current);
    }
}
