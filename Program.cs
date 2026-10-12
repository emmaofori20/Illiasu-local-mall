using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
// CLI output is deliberately sanitized; avoid provider logs dumping SQL diagnostics.
if (args.Contains("--database-provision")) builder.Logging.ClearProviders();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new AdminAreaAuthorizationConvention());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Configure ConnectionStrings:DefaultConnection using development user secrets " +
            "or the ConnectionStrings__DefaultConnection environment variable.");
    }

    options.UseSqlServer(connectionString);
    if (args.Contains("--database-provision"))
        options.AddInterceptors(new RejectImplicitDatabaseCreation());
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    // Email delivery/verification will be a separate implementation slice.
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-ILLiasu.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-ILLiasu.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppRoles.AdminPolicy, policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy(AppRoles.CategoryPolicy, policy => policy.RequireRole(AppRoles.Admin, AppRoles.InventoryManager));
    options.AddPolicy(AppRoles.ProductPolicy, policy => policy.RequireRole(AppRoles.Admin, AppRoles.InventoryManager));
});
builder.Services.AddScoped<ILLiasu_Local_Mall.Application.Catalog.CategoryManagement>();
builder.Services.AddScoped<ILLiasu_Local_Mall.Application.Catalog.ProductManagement>();
builder.Services.AddScoped<ILLiasu_Local_Mall.Application.Catalog.ProductCreation>();
builder.Services.AddScoped<ILLiasu_Local_Mall.Application.Catalog.ProductEditing>();
builder.Services.AddSingleton<ILLiasu_Local_Mall.Infrastructure.Storage.IProductImageUploadBoundary, ILLiasu_Local_Mall.Infrastructure.Storage.DisabledProductImageUploadBoundary>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(10), QueueLimit = 0
        }));
});

var app = builder.Build();

if (args.Contains("--database-provision"))
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Environment.ExitCode = await DatabaseProvisioning.RunAsync(app.Services, app.Environment,
        app.Configuration, args, Console.Out, cancellation.Token);
    await app.DisposeAsync();
    return;
}

if (args.Contains("--seed-catalogue-units"))
{
    IdentityProvisioning.RequireLocalDevelopment(app.Environment, app.Configuration.GetConnectionString("DefaultConnection"));
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var added = await UnitReferenceData.SeedAsync(db);
    Console.WriteLine($"Selling units: {added} added; existing definitions preserved.");
    return;
}

if (args.Contains("--seed-identity") || args.Contains("--provision-admin"))
{
    await IdentityProvisioning.RunAsync(app.Services, app.Environment, app.Configuration,
        args.Contains("--provision-admin"));
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// The visual prototype remains Development-only and is now role-protected.
if (app.Environment.IsDevelopment())
{
    app.MapControllerRoute(name: "admin-prototype", pattern: "{area:exists}/{controller=Overview}/{action=Index}/{id?}");
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
