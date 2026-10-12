using ILLiasu_Local_Mall.Domain;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name, ContentRootPath = root, EnvironmentName = "Development" });
builder.Logging.ClearProviders();
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
await using var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
var before = await db.UnitsOfMeasure.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
Check(await UnitReferenceData.SeedAsync(db) == 0, "Repeat seeding adds no duplicate codes");
var after = await db.UnitsOfMeasure.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
Check(System.Text.Json.JsonSerializer.Serialize(before) == System.Text.Json.JsonSerializer.Serialize(after), "Existing unit definitions, identifiers and activation preserved");
var codes = new[] { "PCS", "PACK", "CTN", "TRAY", "BAG", "BTL", "KG", "G", "L", "ML" };
Check(codes.All(c => after.Any(u => u.Code == c && u.IsActive)), "All ten active reference units exist");
foreach (var u in after.Where(u => codes.Contains(u.Code)))
{
    var measured = u.Kind is UnitOfMeasureKind.Mass or UnitOfMeasureKind.Volume;
    Check(u.DecimalPlaces == (measured ? 3 : 0), u.Code + " quantity precision");
    Check(StockConsumptionRules.Validate(measured ? .001m : 1m, 1, u.DecimalPlaces, measured) == null, u.Code + " supported quantity accepted");
    Check(StockConsumptionRules.Validate(measured ? .0001m : .5m, 1, u.DecimalPlaces, measured) != null, u.Code + " excess precision rejected");
}
Check(after.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == after.Count, "Unique unit codes");
