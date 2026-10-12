using Microsoft.AspNetCore.Mvc;
using ILLiasu_Local_Mall.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using ILLiasu_Local_Mall.Infrastructure.Identity;

namespace ILLiasu_Local_Mall.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/Overview")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OverviewController : Controller
{
    // Explicitly enabled for authenticated Admin users in all environments.
    // The view clearly identifies its presentation-only sample data.
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index(int period = 30) => View(DashboardPrototype.Create(period));
}
