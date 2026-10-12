using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var environment = new TestEnvironment();
const string local = "Server=Emmanuel;Database=IlliasuLocalMallDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
const string production = "Server=example.invalid;Database=ApprovedApp;User ID=never-log-this-user;Encrypt=True;TrustServerCertificate=False";
var count = 0;
void Check(bool condition, string name)
{ if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS: " + name); }
void Refuses(Action action, string name)
{ try { action(); throw new Exception("Unexpected acceptance: " + name); } catch (ProvisioningSafetyException) { Check(true, name); } }
DatabaseProvisioningOptions Parse(params string[] flags) => DatabaseProvisioningOptions.Parse(["--database-provision", .. flags]);
Check(!Parse().Apply, "Default is read-only");
Check(Parse("--check").Validate(environment, local).InitialCatalog == "IlliasuLocalMallDb", "Approved local check allowed");
Refuses(() => Parse("--check", "--apply"), "Conflicting modes rejected");
Refuses(() => Parse("--seed-reference-data"), "Seed requires apply");
Refuses(() => Parse("--create-local-database"), "Creation requires apply");
Refuses(() => Parse("--apply", "--seed-only"), "Seed-only requires explicit reference seeding");
Refuses(() => Parse("--seed-identity"), "Legacy command mixing rejected");
Refuses(() => Parse("--check", "--check"), "Duplicate flags rejected");
Refuses(() => Parse("--confirm-server"), "Missing target value rejected");
Refuses(() => Parse("--apply").Validate(environment, local), "Apply requires target confirmations");
Refuses(() => Parse().Validate(environment, local.Replace("IlliasuLocalMallDb", "Other")), "Unauthorized local database rejected");
Refuses(() => Parse().Validate(environment, local.Replace("Emmanuel", "example.invalid")), "Remote Development target rejected");
Refuses(() => Parse().Validate(environment, local.Replace("IlliasuLocalMallDb", "master")), "System database rejected");
Refuses(() => Parse().Validate(environment, "bad connection descriptor"), "Malformed descriptor rejected without details");
var localApply = Parse("--apply", "--confirm-server", "Emmanuel", "--confirm-database", "IlliasuLocalMallDb", "--create-local-database");
Check(localApply.Validate(environment, local).IntegratedSecurity, "Confirmed local creation allowed by guard");
environment.EnvironmentName = "Production";
Refuses(() => Parse().Validate(environment, production), "Production refuses implicit opt-in");
Refuses(() => Parse("--allow-production").Validate(environment, production), "Production check requires confirmations");
var prodCheck = Parse("--check", "--allow-production", "--confirm-server", "example.invalid", "--confirm-database", "ApprovedApp");
Check(prodCheck.Validate(environment, production).InitialCatalog == "ApprovedApp", "Confirmed Production check validates without connecting");
Refuses(() => prodCheck.Validate(environment, production.Replace("TrustServerCertificate=False", "TrustServerCertificate=True")), "Production certificate bypass rejected");
Refuses(() => prodCheck.Validate(environment, production.Replace("Encrypt=True", "Encrypt=False")), "Unencrypted Production refused");
Refuses(() => prodCheck.Validate(environment, production.Replace("ApprovedApp", "Wrong")), "Wrong target confirmation rejected");
Refuses(() => (prodCheck with { Apply = true, CreateLocalDatabase = true }).Validate(environment, production), "Production creation forbidden");
environment.EnvironmentName = "Staging";
Refuses(() => prodCheck.Validate(environment, production), "Unknown environment rejected");
Check(DatabaseConnectivity.ClassifyMetadata(null, true).Availability == DatabaseAvailability.Missing, "Missing requires proven metadata visibility");
Check(DatabaseConnectivity.ClassifyMetadata(null, false).Availability == DatabaseAvailability.Unknown, "Hidden metadata does not mean missing");
Check(DatabaseConnectivity.ClassifyMetadata("OFFLINE", true).Availability == DatabaseAvailability.Unavailable, "Offline database is not missing");
Check(DatabaseConnectivity.ClassifyMetadata("ONLINE", false).Availability == DatabaseAvailability.Available, "Visible online database recognized");
Check(!DatabaseConnectivity.MayInspectMissingDatabase([18456, 4060]), "Invalid credentials cannot trigger missing-database path");
Check(!DatabaseConnectivity.MayInspectMissingDatabase([-1, 53, -2]), "Network/TLS/timeout errors cannot trigger missing path");
Check(DatabaseConnectivity.MayInspectMissingDatabase([4060]), "4060 permits metadata inspection only");
var known = new[] { "a", "b", "c" };
DatabaseProvisioning.ValidateHistory(known, []);
DatabaseProvisioning.ValidateHistory(known, ["a", "b"]);
Check(true, "Empty and valid prefix histories accepted");
Refuses(() => DatabaseProvisioning.ValidateHistory(known, ["a", "c"]), "History gap rejected");
Refuses(() => DatabaseProvisioning.ValidateHistory(known, ["a", "unknown"]), "Unknown history rejected");
Refuses(() => DatabaseProvisioning.ValidateHistory([], []), "Missing migration assembly rejected");
Refuses(() => RejectImplicitDatabaseCreation.ValidateSql("CREATE\n DATABASE [Anything];"), "EF implicit creation blocked");
RejectImplicitDatabaseCreation.ValidateSql("CREATE TABLE [Approved] ([Id] int);");
Check(true, "Table migrations remain allowed");
// A service collection with no DB context proves refusals/help happen before DB access.
using var services = new ServiceCollection().BuildServiceProvider();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = production }).Build();
environment.EnvironmentName = "Production";
var output = new StringWriter();
Check(await DatabaseProvisioning.RunAsync(services, environment, config, ["--database-provision", "--apply"], output) == 2,
    "Run refuses unauthorized production before resolving database services");
Check(!output.ToString().Contains("never-log-this-user") && !output.ToString().Contains("User ID"), "Guard logs do not disclose credentials");
Check(await DatabaseProvisioning.RunAsync(services, environment, config, ["--database-provision", "--help"], TextWriter.Null) == 0, "Help needs no database or configuration");
var seedSql = ReferenceDataSql.Generate();
Check(seedSql.Split("IF NOT EXISTS").Length - 1 == 17, "SQL fallback has three migration prerequisites and fourteen add-only definitions");
Check(!seedSql.Contains("AspNetUsers") && !seedSql.Contains("Password"), "SQL fallback creates no users or credentials");
Check(seedSql == ReferenceDataSql.Generate(), "Offline reference SQL generation is deterministic");
Console.WriteLine($"{count} offline safety/export checks passed. No database connections made.");

if (args.Contains("--local-integration"))
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    { ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name, ContentRootPath = root, EnvironmentName = "Development" });
    builder.Logging.ClearProviders();
    var connection = builder.Configuration.GetConnectionString("DefaultConnection");
    IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection).AddInterceptors(new RejectImplicitDatabaseCreation()));
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
    await using var app = builder.Build();
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Existing approved target only. Never create/drop a test database or use a hosted endpoint.
    var historyBefore = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    Check(historyBefore.Length == 3 && !(await db.Database.GetPendingMigrationsAsync()).Any(), "Existing approved local database has full chain");
    var usersBefore = await db.Users.CountAsync();
    Check(await DatabaseProvisioning.RunAsync(app.Services, app.Environment, app.Configuration,
        ["--database-provision", "--check"], Console.Out) == 0, "Local read-only connectivity/history check succeeds");
    string[] apply = ["--database-provision", "--apply", "--seed-reference-data", "--confirm-server", "Emmanuel", "--confirm-database", "IlliasuLocalMallDb"];
    Check(await DatabaseProvisioning.RunAsync(app.Services, app.Environment, app.Configuration, apply, Console.Out) == 0, "Local explicit migration/seed workflow succeeds");
    var unitsBeforeRepeat = System.Text.Json.JsonSerializer.Serialize(await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Code).ToListAsync());
    var rolesBeforeRepeat = System.Text.Json.JsonSerializer.Serialize(await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync());
    Check(await DatabaseProvisioning.RunAsync(app.Services, app.Environment, app.Configuration, [.. apply, "--seed-only"], Console.Out) == 0, "Seed-only repeat succeeds");
    Check(unitsBeforeRepeat == System.Text.Json.JsonSerializer.Serialize(await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Code).ToListAsync()), "Repeat preserves unit IDs/configuration");
    Check(rolesBeforeRepeat == System.Text.Json.JsonSerializer.Serialize(await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync()), "Repeat preserves role IDs/definitions");
    Check(await db.Users.CountAsync() == usersBefore, "No accounts created");
    Check(historyBefore.SequenceEqual(await db.Database.GetAppliedMigrationsAsync()), "Migration history unchanged on fully migrated database");
    Check(await db.Roles.CountAsync(r => AppRoles.All.Contains(r.Name!)) == 4, "Four approved roles present");
    Check(await db.UnitsOfMeasure.CountAsync() >= UnitReferenceData.Definitions.Count, "Reference units present");
    await db.Database.ExecuteSqlRawAsync(seedSql);
    await db.Database.ExecuteSqlRawAsync(seedSql);
    Check(unitsBeforeRepeat == System.Text.Json.JsonSerializer.Serialize(await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Code).ToListAsync()), "Repeated SQL fallback preserves existing units");
    Check(rolesBeforeRepeat == System.Text.Json.JsonSerializer.Serialize(await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync()), "Repeated SQL fallback preserves existing roles");
    Check(await db.Users.CountAsync() == usersBefore, "SQL fallback creates no accounts");
}

sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "DatabaseProvisioningSmoke";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
