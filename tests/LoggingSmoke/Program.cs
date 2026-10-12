using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using ILLiasu_Local_Mall.Controllers;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Logging;
using ILLiasu_Local_Mall.ViewModels.Account;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Json;
using Serilog.Core;
using Serilog.Events;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../obj/logging-smoke"));
Directory.CreateDirectory(root);
var run = "run-" + Guid.NewGuid().ToString("N");
var directory = Path.Combine(root, "App_Data", run);
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
void Refuses(Action action, string name) { try { action(); throw new Exception(name); } catch (InvalidOperationException) { Check(true, name); } }
Refuses(() => ApplicationLogging.ResolveDirectory(root, null, "wwwroot/logs"), "Public storage rejected");
Refuses(() => ApplicationLogging.ResolveDirectory(root, null, "../outside"), "Storage escaping App_Data rejected");
Refuses(() => ApplicationLogging.ResolveDirectory(root, Path.Combine(root, "App_Data"), "App_Data/logs"), "Custom public web root cannot contain logs");
Check(ApplicationLogging.ResolveDirectory(root, null, "App_Data/logs") == Path.Combine(root, "App_Data", "logs"), "Default is non-public App_Data");

using (var logger = ApplicationLogging.Create(root, null, directory))
{
    logger.ForContext("Email", "email-canary@example.invalid").ForContext("Password", "password-canary")
        .ForContext("Cookie", "cookie-canary").ForContext("RequestPath", "/path-canary")
        .ForContext("RequestId", "injected-request-canary")
        .Error(new InvalidOperationException("exception-secret-canary"), "raw-message-canary {ConnectionString}", "connection-canary");
    logger.Warning("Diagnostic {DiagnosticEvent} {IdentityOperation} {Outcome} {IdentityCodes}",
        "IdentityOutcome", "Login", "Rejected", new[] { "DuplicateEmail", "code-personal-canary" });
    using var factory = new SerilogLoggerFactory(logger, dispose: false);
    var userManager = new EmptyUserManager();
    var controller = new AccountController(userManager, null!, null!, factory.CreateLogger<AccountController>());
    var result = await controller.Login(new LoginViewModel { Email = "login-email-canary@example.invalid", Password = "login-password-canary", ReturnUrl = "/?token=return-canary" });
    Check(result is ViewResult && !controller.ModelState.IsValid, "Empty user store rejects login with existing generic UI behavior");

    // A closed loopback port deliberately fails; never use local/hosted SQL credentials.
    SqlException? sqlError = null;
    try
    {
        await using var sql = new SqlConnection("Server=127.0.0.1,1;Database=NoDatabase;Integrated Security=True;Encrypt=True;Connect Timeout=1;ConnectRetryCount=0;Pooling=False");
        await sql.OpenAsync();
    }
    catch (SqlException error) { sqlError = error; }
    Check(sqlError != null, "Loopback connectivity failure captured without database writes");

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Production" });
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog(logger, dispose: false);
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build();
    app.UseMiddleware<RequestOutcomeLogging>();
    app.UseExceptionHandler(handler => handler.Run(context => { context.Response.StatusCode = 500; return context.Response.WriteAsync("Error"); }));
    app.MapGet("/success", () => "ok");
    app.MapGet("/failure", (HttpContext _) => throw new InvalidOperationException("http-secret-canary"));
    app.MapPost("/sql-failure", (HttpContext _) => throw sqlError!);
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    Check((await client.GetAsync("/success?token=query-secret-canary")).StatusCode == HttpStatusCode.OK, "HTTP success recorded");
    Check((await client.GetAsync("/failure")).StatusCode == HttpStatusCode.InternalServerError, "Exception remains generic HTTP 500");
    Check((await client.PostAsync("/sql-failure", new StringContent("password=body-secret-canary"))).StatusCode == HttpStatusCode.InternalServerError, "SQL request failure remains HTTP 500");
    Check((await client.GetAsync("/App_Data/" + run + "/anything.jsonl")).StatusCode == HttpStatusCode.NotFound, "Log paths are not HTTP endpoints");
    await app.StopAsync();
}
var text = string.Join('\n', Directory.GetFiles(directory, "*.jsonl").Select(File.ReadAllText));
Check(!text.Contains("canary", StringComparison.Ordinal), "No message/exception/email/password/token/cookie/path/body canaries persisted");
var records = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line)).ToArray();
Check(records.Length >= 8, "Structured JSON file events exist");
Check(records.Any(doc => doc.RootElement.GetProperty("Properties").TryGetProperty("SqlErrors", out _)), "SQL numbers/states retained without SQL text");
Check(records.Any(doc => doc.RootElement.GetProperty("Properties").TryGetProperty("ExceptionTypes", out _)), "Exception type summaries retained");
Check(records.Count(doc => doc.RootElement.GetProperty("Properties").GetProperty("DiagnosticEvent").GetString() == "HttpRequestCompleted") == 4,
    "One final HTTP outcome per request, including errors and 404");
Check(records.Any(doc => doc.RootElement.GetProperty("Properties").GetProperty("DiagnosticEvent").GetString() == "IdentityOutcome"), "Identity rejection summary persisted");
Check(records.All(doc => !doc.RootElement.TryGetProperty("Exception", out _)), "Raw exceptions are never serialized");
foreach (var doc in records) doc.Dispose();

var blocked = Path.Combine(root, "App_Data", "blocked-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.GetDirectoryName(blocked)!);
File.WriteAllText(blocked, "A file prevents directory creation.");
using (var fallback = ApplicationLogging.Create(root, null, blocked))
    fallback.Information("Diagnostic {DiagnosticEvent}", "ApplicationStarted");
Check(true, "Unwritable log directory does not terminate logging caller");
using (var badPath = ApplicationLogging.Create(root, null, "wwwroot/logs"))
    badPath.Information("Diagnostic {DiagnosticEvent}", "ApplicationStarted");
Check(!Directory.Exists(Path.Combine(root, "wwwroot", "logs")), "Unsafe path falls back without creating public files");
using (var throwing = new LoggerConfiguration().AuditTo.Sink(new ThrowingSink()).CreateLogger())
using (var protectedLogger = new LoggerConfiguration().WriteTo.Sink(new SafeDiagnosticSink(throwing)).CreateLogger())
    protectedLogger.Warning("Diagnostic {DiagnosticEvent}", "ApplicationStarted");
Check(true, "A throwing sink cannot terminate the application caller");

var rotation = Path.Combine(root, "App_Data", "rotation-" + Guid.NewGuid().ToString("N"));
var oldOut = Console.Out;
try
{
    Console.SetOut(TextWriter.Null);
    using var logger = ApplicationLogging.Create(root, null, rotation);
    for (var i = 0; i < 100_000; i++)
        logger.Information("Diagnostic {DiagnosticEvent} {Method} {StatusCode} {ElapsedMilliseconds}", "HttpRequestCompleted", "GET", 200, i);
}
finally { Console.SetOut(oldOut); }
var files = Directory.GetFiles(rotation, "*.jsonl");
Check(files.Length == ApplicationLogging.RetainedFiles, "Size rotation retains at most eight files");
Check(files.All(file => new FileInfo(file).Length <= ApplicationLogging.FileSizeLimit + 4096), "Each rolled file stays within 2 MiB plus event boundary");

// Actual MVC app in Production, in-memory HTTPS transport; SQL points only to a closed loopback port.
// No application database is touched and no hosted credentials are needed.
var appRun = "App_Data/logs/mvc-test-" + Guid.NewGuid().ToString("N");
var priorDirectory = Environment.GetEnvironmentVariable("Diagnostics__Directory");
var priorAsp = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var priorDot = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var priorConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
string? mvcRoot = null;
try
{
    Environment.SetEnvironmentVariable("Diagnostics__Directory", appRun);
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=127.0.0.1,1;Database=NoDatabase;Integrated Security=True;Encrypt=True;Connect Timeout=1;ConnectRetryCount=0;Pooling=False");
    await using var factory = new WebApplicationFactory<HomeController>().WithWebHostBuilder(builder =>
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")));
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
    });
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    mvcRoot = factory.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
    Check((await client.GetAsync("/")).StatusCode == HttpStatusCode.OK, "Real Production MVC homepage renders");
    var login = await client.GetStringAsync("/Account/Login?returnUrl=/?token=mvc-query-canary");
    Check(login.Contains("__RequestVerificationToken"), "Real Production login renders with secure antiforgery");
    var match = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    var post = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
        ["Email"] = "mvc-email-canary@example.invalid", ["Password"] = "mvc-password-canary", ["ReturnUrl"] = "/?token=mvc-return-canary"
    }));
    Check(post.StatusCode == HttpStatusCode.InternalServerError, "Real MVC SQL failure uses production exception handler");
    Check(!((await post.Content.ReadAsStringAsync()).Contains("SqlException")), "Production error page does not expose exception details");
    Check((await client.GetAsync("/App_Data/logs/app-20261012.jsonl")).StatusCode == HttpStatusCode.NotFound, "Actual MVC app cannot serve private log path");
    Check((await client.GetAsync("/Admin/Overview")).StatusCode == HttpStatusCode.NotFound, "Production admin restriction retained");
}
finally
{
    Environment.SetEnvironmentVariable("Diagnostics__Directory", priorDirectory);
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", priorAsp);
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", priorDot);
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", priorConnection);
}
var mvcText = string.Join('\n', Directory.GetFiles(Path.Combine(mvcRoot!, appRun), "*.jsonl").Select(File.ReadAllText));
Check(!mvcText.Contains("canary", StringComparison.Ordinal), "Actual MVC logs omit credentials/query/form canaries");
Check(mvcText.Contains("ApplicationStarted") && mvcText.Contains("ApplicationStopping") && mvcText.Contains("ApplicationStopped"), "Actual startup and graceful shutdown lifecycle events persisted");
Check(mvcText.Contains("SqlErrors") && mvcText.Contains("Account.Login"), "Actual login SQL failure retains safe endpoint and SQL diagnostics");
Console.WriteLine($"{checks} logging checks passed. Test artifacts: {root}");

sealed class EmptyUserManager() : UserManager<ApplicationUser>(new EmptyStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
    new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
    new ServiceCollection().BuildServiceProvider(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<ApplicationUser>>.Instance)
{
    public override Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser?>(null);
}
sealed class ThrowingSink : ILogEventSink
{
    public void Emit(LogEvent value) => throw new IOException("sink-failure-private-canary");
}
sealed class EmptyStore : IUserStore<ApplicationUser>
{
    public void Dispose() { }
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.NormalizedUserName = name; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
}
