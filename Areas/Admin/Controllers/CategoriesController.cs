using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Areas.Admin.ViewModels;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ILLiasu_Local_Mall.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = AppRoles.CategoryPolicy), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CategoriesController(CategoryManagement categories, IWebHostEnvironment environment) : Controller
{
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        // Keep the existing administration deployment boundary until production rollout.
        if (!environment.IsDevelopment()) context.Result = NotFound();
        ViewData["LiveCategories"] = true;
        base.OnActionExecuting(context);
    }
    public async Task<IActionResult> Index(string? q, string status = "all", int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var result = await categories.ListAsync(q, status, page, pageSize, ct);
        return View(new CategoryList(result.Items, result.Search, result.Status, result.Page, result.Pages, result.Total, result.PageSize));
    }
    [HttpGet] public IActionResult Create() => View("Form", new CategoryForm());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Slug")] CategoryForm form, CancellationToken ct)
    {
        DiscardRawFieldValidation();
        if (!ModelState.IsValid) return View("Form", form);
        var result = await categories.CreateAsync(form.Name, form.Slug, ct);
        ApplyResult(form, result);
        if (result.Outcome == CategoryWriteOutcome.ValidationFailed) return View("Form", form);
        TempData["CategorySuccess"] = "Category created and activated.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var item = await categories.FindAsync(id, ct);
        return item is null ? NotFound() : View("Form", Form(item));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Name,Slug,Snapshot")] CategoryForm form, CancellationToken ct)
    {
        var current = await categories.FindAsync(id, ct);
        if (current is null) return NotFound();
        form.Id = id; form.Products = current.Products;
        var old = categories.Read(form.Snapshot, id);
        if (old is null) return BadRequest("Invalid category edit token. Reload the form.");
        DiscardRawFieldValidation();
        if (!ModelState.IsValid) return View("Form", form);
        var result = await categories.EditAsync(old, form.Name, form.Slug, ct);
        ApplyResult(form, result);
        if (result.Outcome == CategoryWriteOutcome.NotFound) return NotFound();
        if (result.Outcome == CategoryWriteOutcome.ValidationFailed) return View("Form", form);
        if (result.Outcome == CategoryWriteOutcome.Conflict) return EditConflict(form, result.Current);
        TempData["CategorySuccess"] = "Category details saved. Activation and product links are unchanged.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        var item = await categories.FindAsync(id, ct);
        return item is null ? NotFound() : View(new CategoryStatusForm { Category = item, Snapshot = categories.Sign(item) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(Guid id, [Bind("Snapshot,Confirm")] CategoryStatusForm form, CancellationToken ct)
    {
        var current = await categories.FindAsync(id, ct);
        if (current is null) return NotFound();
        var old = categories.Read(form.Snapshot, id);
        if (old is null) return BadRequest("Invalid category confirmation token. Reload the form.");
        form.Category = current;
        if (!form.Confirm) { ModelState.AddModelError("Confirm", "Confirm that you understand the visibility impact."); return View(form); }
        var result = await categories.ChangeStatusAsync(old, ct);
        if (result.Outcome == CategoryWriteOutcome.NotFound) return NotFound();
        if (result.Outcome == CategoryWriteOutcome.AlreadyApplied)
        { TempData["CategorySuccess"] = "The requested status is already applied."; return RedirectToAction(nameof(Index)); }
        if (result.Outcome == CategoryWriteOutcome.Conflict)
        {
            current = result.Current;
            if (current is null) return NotFound();
            form.Category = current; form.Snapshot = categories.Sign(current); form.Confirm = false;
            ModelState.Remove("Snapshot"); ModelState.Remove("Confirm");
            ModelState.AddModelError("", "This category or its product links changed. Review the current details and confirm again.");
            Response.StatusCode = 409; return View(form);
        }
        TempData["CategorySuccess"] = old.IsActive ? "Category deactivated. Product references are preserved." : "Category activated. Product availability flags are unchanged.";
        return RedirectToAction(nameof(Index));
    }
    private IActionResult EditConflict(CategoryForm form, CategorySnapshot? current)
    {
        if (current is null) return NotFound();
        form.Snapshot = categories.Sign(current); ModelState.Remove("Snapshot");
        ModelState.AddModelError("", $"Another user changed this category. Current name: {current.Name}; slug: {current.Slug}. Review your entries before saving again.");
        Response.StatusCode = 409; return View("Form", form);
    }
    private void DiscardRawFieldValidation()
    {
        // The service validates after normalization; raw MVC metadata errors must not
        // reject valid uppercase/padded input. Other binding errors remain authoritative.
        ModelState.Remove("Name"); ModelState.Remove("Slug");
    }
    private void ApplyResult(CategoryForm form, CategoryWriteResult result)
    {
        form.Name = result.Name; form.Slug = result.Slug;
        foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
    private CategoryForm Form(CategorySnapshot x) => new() { Id = x.Id, Name = x.Name, Slug = x.Slug, Products = x.Products, Snapshot = categories.Sign(x) };
}
