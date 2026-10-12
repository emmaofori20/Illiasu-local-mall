using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ILLiasu_Local_Mall.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using ILLiasu_Local_Mall.Infrastructure.Identity;

namespace ILLiasu_Local_Mall.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OverviewController(IWebHostEnvironment environment) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Role authorization now protects access; sample fixtures remain Development-only.
        if (!environment.IsDevelopment()) context.Result = NotFound();
        base.OnActionExecuting(context);
    }

    public IActionResult Index(int period = 30) => View(DashboardPrototype.Create(period));
}
