using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using ILLiasu_Local_Mall.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ILLiasu_Local_Mall.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, ApplicationDbContext db, ILogger<AccountController> logger) : Controller
{
    [Authorize]
    public IActionResult Index() => View();

    [AllowAnonymous, HttpGet]
    public IActionResult Register(string? returnUrl = null) => View(new RegisterViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, EnableRateLimiting("account")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        using var diagnosticScope = logger.BeginScope(new Dictionary<string, object> { ["IdentityOperation"] = "Registration" });
        if (!ModelState.IsValid) { LogOutcome("Registration", "InvalidInput"); return View(model); }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = model.Email.Trim(), Email = model.Email.Trim() };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            LogOutcome("Registration", "Rejected", result.Errors.Select(e => e.Code).ToArray());
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Code.StartsWith("Password") ? error.Description : "Unable to create this account. Check your details or try signing in.");
            return View(model);
        }
        using (logger.BeginScope(new Dictionary<string, object> { ["IdentityOperation"] = "CustomerRoleAssignment" }))
        {
            result = await users.AddToRoleAsync(user, AppRoles.Customer);
            if (!result.Succeeded)
            {
                LogOutcome("CustomerRoleAssignment", "Rejected", result.Errors.Select(e => e.Code).ToArray());
                ModelState.AddModelError("", "Registration is unavailable. Please try again later.");
                return View(model); // Transaction rollback prevents a roleless registration.
            }
        }
        await transaction.CommitAsync();
        await signIn.SignInAsync(user, isPersistent: false);
        LogOutcome("Registration", "Succeeded");
        return SafeRedirect(model.ReturnUrl);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, EnableRateLimiting("account")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        using var diagnosticScope = logger.BeginScope(new Dictionary<string, object> { ["IdentityOperation"] = "Login" });
        if (!ModelState.IsValid) { LogOutcome("Login", "InvalidInput"); return View(model); }
        var user = await users.FindByEmailAsync(model.Email.Trim());
        var result = user is null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        LogOutcome("Login", result.Succeeded ? "Succeeded" : result.IsLockedOut ? "LockedOut"
            : result.IsNotAllowed ? "NotAllowed" : result.RequiresTwoFactor ? "RequiresTwoFactor" : "Rejected");
        if (result.Succeeded)
        {
            if (await users.IsInRoleAsync(user!, AppRoles.Admin))
                return RedirectToAction("Index", "Overview", new { area = "Admin" });
            return SafeRedirect(model.ReturnUrl);
        }
        ModelState.AddModelError("", "Unable to sign in. Check your details or try again later.");
        return View(model);
    }

    [Authorize, HttpGet]
    public IActionResult Logout() => View();

    [Authorize, HttpPost, ActionName("Logout")]
    public async Task<IActionResult> LogoutConfirmed()
    {
        await signIn.SignOutAsync();
        LogOutcome("Logout", "Succeeded");
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult SafeRedirect(string? returnUrl) => Url.IsLocalUrl(returnUrl)
        ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));

    private void LogOutcome(string operation, string outcome, string[]? codes = null) =>
        logger.Log(outcome == "Succeeded" ? LogLevel.Information : LogLevel.Warning,
            "Diagnostic {DiagnosticEvent} {IdentityOperation} {Outcome} {IdentityCodes}",
            "IdentityOutcome", operation, outcome, codes ?? []);
}
