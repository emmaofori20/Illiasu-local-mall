using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Areas.Admin.ViewModels;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using ILLiasu_Local_Mall.Infrastructure.Storage;

namespace ILLiasu_Local_Mall.Areas.Admin.Controllers;
[Area("Admin"), Authorize(Policy = AppRoles.ProductPolicy), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProductsController(ProductManagement products, ProductCreation creation, ProductEditing editing, ApplicationDbContext db, IProductImageUploadBoundary uploads, IWebHostEnvironment environment, ILogger<ProductsController> logger) : Controller
{
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (!environment.IsDevelopment()) context.Result = NotFound();
        ViewData["LiveProducts"] = true; base.OnActionExecuting(context);
    }
    public async Task<IActionResult> Index(string? q, Guid? categoryId, string status = "all", int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) { Response.StatusCode = 400; ModelState.AddModelError("", "One or more filters are invalid. Clear the filters and try again."); }
        try { return View(await products.ListAsync(q, categoryId, status, page, pageSize, ct)); }
        catch (SqlException e)
        {
            logger.LogError("Product listing database failure. SQL error code {Code}.", e.Number);
            Response.StatusCode = 503;
            return View(new ProductList([], [], (q ?? "")[..Math.Min(q?.Length ?? 0,100)], categoryId, "all", 1, 1, 0, 10, true));
        }
    }
    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct)
    {
        var form=new ProductCreateForm();await Populate(form,ct);return View(form);
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(28*1024*1024), RequestFormLimits(MultipartBodyLengthLimit=26*1024*1024,ValueCountLimit=2048)]
    public async Task<IActionResult> Create([Bind("Name,Slug,CategoryId,Description,IsActive,Variants,Images,PrimaryImageIndex")] ProductCreateForm form, CancellationToken ct)
    {
        ProductCreation.Normalize(form);
        foreach(var key in ModelState.Keys.Where(k=>k is "Name" or "Slug" or "Description" || k.EndsWith(".Name") || k.EndsWith(".Sku") || k.EndsWith(".Barcode")).ToArray()) ModelState.Remove(key);
        TryValidateModel(form);
        for(var i=0;i<Math.Min(form.Variants.Count,25);i++)
            foreach(var required in new[] { "Price", "QuantityStep", "ClientRowKey" })
                if(!Request.Form.ContainsKey($"Variants[{i}].{required}")) ModelState.AddModelError($"Variants[{i}].{required}","Enter an explicit value.");
        foreach(var message in uploads.Validate(form.Images,form.PrimaryImageIndex)) ModelState.AddModelError("Images",message);
        if(ModelState.IsValid)
        {
            try {
                var result=await creation.CreateAsync(form,ct);
                if(result.Id.HasValue) { TempData["CategorySuccess"]="Product created with its selling options and stock rules.";return RedirectToAction(nameof(Index)); }
                foreach(var error in result.Errors) ModelState.AddModelError(error.Field,error.Message);
            } catch(SqlException e) {
                logger.LogError("Product creation database failure. SQL code {Code}.",e.Number);
                ModelState.AddModelError("","The database could not complete this request. Your entries are retained. Check the listing before retrying if the result is uncertain.");
            }
        }
        await Populate(form,ct);return View(form);
    }
    private async Task Populate(ProductCreateForm form,CancellationToken ct)
    {
        form.Categories=await db.Categories.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).Select(x=>new CategoryOption(x.Id,x.Name,x.IsActive)).ToListAsync(ct);
        form.Units=await db.UnitsOfMeasure.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync(ct);
        ViewData["CreateUnits"]=form.Units;
        if(form.Variants.Count==0) form.Variants.Add(new());
        ViewData["CreateOptions"]=form.Variants;
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Variants).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();
        var form = new ProductEditForm { Id = id, Name = product.Name, Slug = product.Slug, Description = product.Description,
            CategoryId = product.CategoryId, IsActive = product.IsActive, Version = Convert.ToBase64String(product.RowVersion),
            ExistingVariants = product.Variants.OrderBy(v => v.Name).ThenBy(v => v.Id).Select(v => new ExistingVariantEdit {
                Id = v.Id, Version = Convert.ToBase64String(v.RowVersion), Price = v.Price, Barcode = v.Barcode, IsActive = v.IsActive, IsAvailable = v.IsAvailable }).ToList() };
        await PopulateEdit(form, product.CategoryId, ct); return View(form);
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(28*1024*1024), RequestFormLimits(ValueCountLimit=2048)]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Version,Name,Description,CategoryId,IsActive,ExistingVariants,Variants")] ProductEditForm form, CancellationToken ct)
    {
        if (id != form.Id) return BadRequest();
        var current = await db.Products.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.Slug, p.CategoryId }).SingleOrDefaultAsync(ct);
        if (current is null) return NotFound();
        form.Slug = current.Slug; ModelState.Remove("Slug");
        ProductCreation.Normalize(form); form.ExistingVariants ??= [];
        foreach (var row in form.ExistingVariants) row.Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode.Trim();
        foreach(var key in ModelState.Keys.Where(k=>k is "Name" or "Description" || k.EndsWith(".Name") || k.EndsWith(".Sku") || k.EndsWith(".Barcode")).ToArray()) ModelState.Remove(key);
        TryValidateModel(form);
        for (var i = 0; i < Math.Min(form.ExistingVariants.Count, 25); i++)
            foreach (var required in new[] { "Id", "Version", "Price" })
                if (!Request.Form.ContainsKey($"ExistingVariants[{i}].{required}")) ModelState.AddModelError($"ExistingVariants[{i}].{required}", "Enter an explicit value.");
        for (var i = 0; i < Math.Min(form.Variants.Count, 25); i++)
            foreach (var required in new[] { "ClientRowKey", "QuantityStep", "Price" })
                if (!Request.Form.ContainsKey($"Variants[{i}].{required}")) ModelState.AddModelError($"Variants[{i}].{required}", "Enter an explicit value.");
        if (ModelState.IsValid)
        {
            try
            {
                var result = await editing.EditAsync(form, ct);
                if (result.Id.HasValue) { TempData["CategorySuccess"] = "Product updated. Existing stock rules were preserved."; return RedirectToAction(nameof(Index)); }
                foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
            }
            catch (SqlException e)
            {
                logger.LogError("Product editing database failure. SQL code {Code}.", e.Number);
                ModelState.AddModelError("", "The database could not complete this request. Your entries are retained. Check the listing before retrying if the result is uncertain.");
            }
        }
        await PopulateEdit(form, current.CategoryId, ct); return View(form);
    }
    private async Task PopulateEdit(ProductEditForm form, Guid retainedCategoryId, CancellationToken ct)
    {
        form.Categories = await db.Categories.AsNoTracking().Where(c => c.IsActive || c.Id == retainedCategoryId).OrderBy(c => c.Name)
            .Select(c => new CategoryOption(c.Id, c.Name, c.IsActive)).ToListAsync(ct);
        form.Units = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync(ct);
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.UnitOfMeasure).Where(v => v.ProductId == form.Id).ToListAsync(ct);
        var mappings = await db.VariantStockMappings.AsNoTracking().Where(m => m.ProductId == form.Id).ToDictionaryAsync(m => m.ProductVariantId, ct);
        form.Display = variants.ToDictionary(v => v.Id, v => {
            mappings.TryGetValue(v.Id, out var m); var owner = variants.FirstOrDefault(o => o.Id == m?.StockVariantId);
            return new ExistingVariantDisplay(v, m, owner?.Name ?? "Unknown source", owner?.UnitOfMeasure.Name ?? "unknown unit");
        });
        ViewData["CreateUnits"] = form.Units;
        ViewData["CreateOptions"] = variants.Where(v => mappings.TryGetValue(v.Id, out var m) && m.StockVariantId == v.Id)
            .Select(v => new VariantCreateInput { ClientRowKey = v.Id, Name = v.Name, Sku = v.Sku, UnitOfMeasureId = v.UnitOfMeasureId })
            .Concat(form.Variants).ToList();
    }
    [HttpGet] public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        var product = await products.FindAsync(id, ct);
        return product is null ? NotFound() : View(new ProductStatusForm { Product = product, Snapshot = products.Sign(product) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(Guid id, [Bind("Snapshot,Confirm")] ProductStatusForm form, CancellationToken ct)
    {
        var current = await products.FindAsync(id, ct); if (current is null) return NotFound();
        var old = products.Read(form.Snapshot, id); if (old is null) return BadRequest("Invalid product confirmation. Reload the form.");
        form.Product = current;
        if (!form.Confirm) { ModelState.AddModelError("Confirm", "Confirm that you understand the visibility impact."); return View(form); }
        if (current.IsActive == !old.IsActive) { TempData["CategorySuccess"] = "The requested product status is already applied."; return RedirectToAction(nameof(Index)); }
        if (!await products.ChangeStatusAsync(old, ct))
        {
            current = await products.FindAsync(id, ct); if (current is null) return NotFound();
            form.Product = current; form.Snapshot = products.Sign(current); form.Confirm = false;
            ModelState.Remove("Snapshot"); ModelState.Remove("Confirm");
            ModelState.AddModelError("", "This product or its category changed. Review the current details and confirm again.");
            Response.StatusCode = 409; return View(form);
        }
        TempData["CategorySuccess"] = old.IsActive ? "Product deactivated. Variants and catalogue references are preserved." : "Product activated. Category and variant availability still apply.";
        return RedirectToAction(nameof(Index));
    }
}
