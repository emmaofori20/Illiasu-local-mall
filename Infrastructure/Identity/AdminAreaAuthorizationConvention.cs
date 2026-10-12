using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace ILLiasu_Local_Mall.Infrastructure.Identity;

// Apply to every present and future MVC controller in the Admin Area, not just navigation.
public sealed class AdminAreaAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.RouteValues.TryGetValue("area", out var area) &&
            string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (controller.Attributes.OfType<IAllowAnonymous>().Any() ||
                controller.Actions.Any(action => action.Attributes.OfType<IAllowAnonymous>().Any()))
                throw new InvalidOperationException("Admin Area endpoints cannot allow anonymous access.");
            controller.Filters.Add(new AuthorizeFilter(controller.ControllerType.AsType() == typeof(Areas.Admin.Controllers.CategoriesController)
                ? AppRoles.CategoryPolicy : controller.ControllerType.AsType() == typeof(Areas.Admin.Controllers.ProductsController)
                ? AppRoles.ProductPolicy : AppRoles.AdminPolicy));
        }
    }
}
