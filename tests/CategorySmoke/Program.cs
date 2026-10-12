using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name, ContentRootPath = root, EnvironmentName = "Development" });
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Logging.ClearProviders();
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var prefix = "category-smoke-" + Guid.NewGuid().ToString("N");
var emails = new List<string>();
var cookieJars = new Dictionary<HttpClient, CookieContainer>();
var categoryIds = new List<Guid>();
Guid? productId = null;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); }
string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
HttpClient Client()
{
    var jar = new CookieContainer();
    var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = jar, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress = new("https://localhost:7116") };
    cookieJars.Add(client, jar); return client;
}
async Task<HttpResponseMessage> Post(HttpClient c, string path, string html, Dictionary<string,string> values)
{
    values["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");
    Check(values["__RequestVerificationToken"].Length > 0, "Form has antiforgery token");
    return await c.PostAsync(path, new FormUrlEncodedContent(values));
}
async Task<HttpClient> Login(string role)
{
    var email = prefix + "-" + role + "@example.invalid"; emails.Add(email);
    var password = "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    var user = new ApplicationUser { UserName = email, Email = email };
    Check((await users.CreateAsync(user, password)).Succeeded, "Temporary " + role + " account created");
    Check((await users.AddToRoleAsync(user, role)).Succeeded, "Temporary role assigned");
    var c = Client();
    var response = await Post(c, "/Account/Login", await c.GetStringAsync("/Account/Login"), new() { ["Email"] = email, ["Password"] = password });
    Check(response.StatusCode == HttpStatusCode.Redirect, role + " login");
    return c;
}
try
{
    using var anonymous = Client();
    Check((await anonymous.GetAsync("/Admin/Categories")).Headers.Location?.ToString().Contains("/Account/Login") == true, "Anonymous category access challenged");
    foreach (var role in new[] { AppRoles.Customer, AppRoles.SalesStaff })
    {
        using var denied = await Login(role);
        foreach(var path in new[] { "/Admin/Categories", "/Admin/Categories/Create", "/Admin/Categories/Edit/" + Guid.NewGuid(), "/Admin/Categories/Status/" + Guid.NewGuid() })
            Check((await denied.GetAsync(path)).Headers.Location?.ToString().Contains("/Account/AccessDenied") == true, role + " cannot access " + path);
        var login = await denied.GetStringAsync("/Account/Login");
        foreach(var path in new[] { "/Admin/Categories/Create", "/Admin/Categories/Edit/" + Guid.NewGuid(), "/Admin/Categories/Status/" + Guid.NewGuid() })
            Check((await Post(denied, path, login, new())).Headers.Location?.ToString().Contains("/Account/AccessDenied") == true, role + " cannot mutate categories");
    }
    using var admin = await Login(AppRoles.Admin);
    Check((await admin.GetAsync("/Admin/Categories")).StatusCode == HttpStatusCode.OK, "Admin category access");
    using var manager = await Login(AppRoles.InventoryManager);
    Check((await manager.GetAsync("/Admin/Categories")).StatusCode == HttpStatusCode.OK, "InventoryManager category access");
    Check((await manager.GetAsync("/Admin")).Headers.Location?.ToString().Contains("/Account/AccessDenied") == true, "InventoryManager cannot access unrelated admin dashboard");
    Check((await manager.PostAsync("/Admin/Categories/Create", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Mutation without antiforgery rejected");
    var create = await manager.GetStringAsync("/Admin/Categories/Create");
    foreach(var invalid in new[] { new Dictionary<string,string> { ["Name"]="   ", ["Slug"]=prefix }, new() { ["Name"]="Valid", ["Slug"]="bad/slug" }, new() { ["Name"]=new string('x',101), ["Slug"]=prefix }, new() { ["Name"]="Valid", ["Slug"]=new string('x',161) } })
    {
        var response = await Post(manager, "/Admin/Categories/Create", create, invalid);
        Check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("validation-summary-errors"), "Invalid category rejected");
    }
    Check((await Post(manager, "/Admin/Categories/Create", create, new() { ["Name"]="  QA sample produce  ", ["Slug"]=prefix.ToUpperInvariant(), ["IsActive"]="false" })).StatusCode == HttpStatusCode.Redirect, "Category creation and normalization");
    var category = await db.Categories.AsNoTracking().SingleAsync(x => x.Slug == prefix); categoryIds.Add(category.Id);
    Check(category.Name == "QA sample produce" && category.IsActive, "Trimmed fields and activation overposting blocked");
    var duplicate = await Post(admin, "/Admin/Categories/Create", await admin.GetStringAsync("/Admin/Categories/Create"), new() { ["Name"]="Another", ["Slug"]=prefix.ToUpperInvariant() });
    Check((await duplicate.Content.ReadAsStringAsync()).Contains("already used"), "Case-insensitive slug uniqueness");
    var editPath = "/Admin/Categories/Edit/" + category.Id;
    var edit = await manager.GetStringAsync(editPath);
    Check((await Post(manager, editPath, edit, new() { ["Name"]="QA sample groceries", ["Slug"]=prefix, ["Snapshot"]=Field(edit,"Snapshot"), ["IsActive"]="false" })).StatusCode == HttpStatusCode.Redirect, "Category editing");
    var stale = await Post(manager, editPath, edit, new() { ["Name"]="Stale overwrite", ["Slug"]=prefix, ["Snapshot"]=Field(edit,"Snapshot") });
    Check(stale.StatusCode == HttpStatusCode.Conflict, "Stale edit cannot overwrite newer changes");
    Check((await Post(manager, editPath, edit, new() { ["Name"]="Tampered", ["Slug"]=prefix, ["Snapshot"]="bad-token" })).StatusCode == HttpStatusCode.BadRequest, "Tampered snapshot rejected");
    var product = new Product { CategoryId = category.Id, Name = "QA reference only", Slug = prefix, IsActive = true }; db.Products.Add(product); await db.SaveChangesAsync(); productId = product.Id;
    var other = new Category { Name="QA separate category", Slug=prefix+"-other" }; db.Categories.Add(other); await db.SaveChangesAsync(); categoryIds.Add(other.Id);
    edit = await manager.GetStringAsync(editPath);
    var collision = await Post(manager,editPath,edit,new() { ["Name"]="QA collision", ["Slug"]=other.Slug, ["Snapshot"]=Field(edit,"Snapshot") });
    Check(collision.StatusCode==HttpStatusCode.OK && (await collision.Content.ReadAsStringAsync()).Contains("already used"), "Editing rejects another category's slug");
    var invalidEdit = await Post(manager,editPath,edit,new() { ["Name"]=" ", ["Slug"]=prefix, ["Snapshot"]=Field(edit,"Snapshot") });
    Check((await invalidEdit.Content.ReadAsStringAsync()).Contains("validation-summary-errors"), "Editing requires a nonblank name");
    await db.Categories.Where(x=>x.Id==other.Id).ExecuteDeleteAsync();
    var statusPath = "/Admin/Categories/Status/" + category.Id;
    var beforeChange = await manager.GetStringAsync(statusPath);
    await db.Categories.Where(x=>x.Id==category.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Name,"QA reviewed groceries"));
    Check((await Post(manager,statusPath,beforeChange,new() { ["Snapshot"]=Field(beforeChange,"Snapshot"), ["Confirm"]="true" })).StatusCode==HttpStatusCode.Conflict, "Stale status confirmation requires fresh review");
    var confirm = await manager.GetStringAsync(statusPath);
    Check(confirm.Contains("1 linked products") && confirm.Contains("Historical references are preserved"), "Referenced-product visibility warning");
    Check((await Post(manager, statusPath, confirm, new() { ["Snapshot"]=Field(confirm,"Snapshot"), ["Confirm"]="false" })).StatusCode == HttpStatusCode.OK, "Explicit confirmation required");
    var statusValues = new Dictionary<string,string> { ["Snapshot"]=Field(confirm,"Snapshot"), ["Confirm"]="true" };
    Check((await Post(manager, statusPath, confirm, statusValues)).StatusCode == HttpStatusCode.Redirect, "Confirmed deactivation");
    Check((await Post(manager, statusPath, confirm, statusValues)).StatusCode == HttpStatusCode.Redirect, "Duplicate status submission idempotent");
    Check(!await db.Categories.Where(x=>x.Id==category.Id).Select(x=>x.IsActive).SingleAsync() && await db.Products.AnyAsync(x=>x.Id==product.Id && x.CategoryId==category.Id && x.IsActive), "Deactivation preserves product references and flags");
    duplicate = await Post(manager, "/Admin/Categories/Create", create, new() { ["Name"]="Another", ["Slug"]=prefix });
    Check((await duplicate.Content.ReadAsStringAsync()).Contains("already used"), "Inactive slug remains reserved");
    var activate = await manager.GetStringAsync(statusPath);
    Check((await Post(manager,statusPath,activate,new() { ["Snapshot"]=Field(activate,"Snapshot"), ["Confirm"]="true" })).StatusCode == HttpStatusCode.Redirect, "Reactivation");
    var rows = Enumerable.Range(0,21).Select(n=>new Category { Name="QA pagination " + n.ToString("D2"), Slug=prefix+"-"+n }).ToArray(); db.Categories.AddRange(rows); await db.SaveChangesAsync(); categoryIds.AddRange(rows.Select(x=>x.Id));
    var page = await manager.GetStringAsync("/Admin/Categories?q=" + prefix + "&page=2");
    Check(page.Contains("Showing 11") && page.Contains("of 22 categories") && Regex.Matches(page,"<tr>").Count == 11, "Search and deterministic 10-row pagination");
    var larger = await manager.GetStringAsync("/Admin/Categories?q="+prefix+"&pageSize=25&status=active");
    Check(Regex.Matches(larger,"<tr>").Count==23 && larger.Contains("of 22 categories"), "Page size 25 preserves active filtering");
    var invalidSize = await manager.GetStringAsync("/Admin/Categories?q="+prefix+"&pageSize=999");
    Check(Regex.Matches(invalidSize,"<tr>").Count==11, "Unsupported page sizes fall back to 10");
    var empty = await manager.GetStringAsync("/Admin/Categories?q=" + prefix + "-missing"); Check(empty.Contains("No matching categories"), "Filtered empty state");
    Check((await manager.GetAsync("/Admin/Categories/Delete/"+category.Id)).StatusCode == HttpStatusCode.NotFound, "No permanent-delete endpoint");
    Console.WriteLine("Category integration suite passed.");
    if (args.Contains("--preview"))
    {
        var file = Path.Combine(Path.GetTempPath(), "illiasu-category-preview.json");
        File.Delete(file + ".done");
        var cookies = cookieJars[manager].GetCookies(new Uri("https://localhost:7116")).Cast<Cookie>()
            .Select(x => new { name=x.Name, value=x.Value, domain=x.Domain, path=x.Path, secure=x.Secure, httpOnly=x.HttpOnly });
        await File.WriteAllTextAsync(file, System.Text.Json.JsonSerializer.Serialize(new { cookies, id=category.Id, query=prefix }));
        Console.WriteLine("Temporary browser preview ready; fixtures removed on completion or after ten minutes.");
        var deadline=DateTime.UtcNow.AddMinutes(10);
        while(!File.Exists(file+".done") && DateTime.UtcNow<deadline) await Task.Delay(500);
        File.Delete(file); File.Delete(file+".done");
    }
}
finally
{
    if(productId is not null) await db.Products.Where(x=>x.Id==productId).ExecuteDeleteAsync();
    await db.Categories.Where(x=>categoryIds.Contains(x.Id) || x.Slug.StartsWith(prefix)).ExecuteDeleteAsync();
    foreach(var email in emails) { var user=await users.FindByEmailAsync(email); if(user is not null) await users.DeleteAsync(user); }
    Console.WriteLine("Removed this run's category/product/account fixtures only.");
}
