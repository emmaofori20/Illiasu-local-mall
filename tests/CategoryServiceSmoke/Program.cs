using System.Data.Common;
using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name, ContentRootPath = root, EnvironmentName = "Development" });
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
builder.Logging.ClearProviders();
var race = new SlugRace(connection!);
builder.Services.AddDataProtection();
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection).AddInterceptors(race, new EditSlugRace(race)));
builder.Services.AddScoped<CategoryManagement>();
await using var app = builder.Build(); await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var service = scope.ServiceProvider.GetRequiredService<CategoryManagement>();
var prefix = "category-service-smoke-" + Guid.NewGuid().ToString("N");
var ids = new List<Guid>(); Guid? productId = null;
void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
async Task<CategorySnapshot> Create(string name, string slug)
{
    var result = await service.CreateAsync(name, slug);
    Check(result.Outcome == CategoryWriteOutcome.Succeeded && result.Current is not null, "Direct service creation: " + name);
    ids.Add(result.Current!.Id); return result.Current;
}
try
{
    var initial = await Create("  QA Café & provisions  ", "  " + prefix.ToUpperInvariant() + "  ");
    Check(initial.Name == "QA Café & provisions" && initial.Slug == prefix && initial.IsActive, "Service trims names/slugs, lowercases slug and activates creation");
    var invalid = new (string? Name, string? Slug, string Field)[] {
        (null, prefix+"-invalid", "Name"), ("", prefix+"-invalid", "Name"), (" \t\r\n", prefix+"-invalid", "Name"), (new string('x',101),prefix+"-invalid","Name"),
        ("Valid",null,"Slug"), ("Valid"," \t","Slug"), ("Valid",new string('x',161),"Slug"),
        ("Valid","bad/slug","Slug"), ("Valid","bad slug","Slug"), ("Valid","-bad","Slug"), ("Valid","bad-","Slug"),
        ("Valid","bad--slug","Slug"), ("Valid","bad_slug","Slug"), ("Valid","café","Slug"), ("Valid","bad\nslug","Slug")
    };
    foreach(var (name,slug,field) in invalid)
    {
        var create = await service.CreateAsync(name,slug);
        Check(create.Outcome == CategoryWriteOutcome.ValidationFailed && create.Errors.Any(e=>e.Field==field), "Invalid " + field + " cannot bypass direct creation validation");
        var edit = await service.EditAsync(initial,name,slug);
        Check(edit.Outcome == CategoryWriteOutcome.ValidationFailed && edit.Errors.Any(e=>e.Field==field), "Invalid " + field + " cannot bypass direct editing validation");
    }
    Check(await db.Categories.AsNoTracking().CountAsync(c=>c.Slug.StartsWith(prefix))==1 && await service.FindAsync(initial.Id)==initial, "Invalid commands perform no catalogue writes");
    var duplicate = await service.CreateAsync("Other",prefix.ToUpperInvariant());
    Check(duplicate.Outcome==CategoryWriteOutcome.ValidationFailed && duplicate.Errors.Single().Field=="Slug", "Normalized duplicate slug rejected through service");
    var other = await Create(initial.Name,prefix+"-other");
    Check(other.Name==initial.Name, "Duplicate display names remain allowed; only slug is unique");
    Check((await service.EditAsync(other,"Colliding",prefix)).Outcome==CategoryWriteOutcome.ValidationFailed, "Edit cannot take another category's slug");
    var deactivated=await service.ChangeStatusAsync(initial);
    Check(deactivated.Outcome==CategoryWriteOutcome.Succeeded && !deactivated.Current!.IsActive, "Direct deactivation succeeds");
    Check((await service.ChangeStatusAsync(initial)).Outcome==CategoryWriteOutcome.AlreadyApplied, "Duplicate status command does not toggle back");
    Check((await service.CreateAsync("Duplicate inactive",prefix)).Outcome==CategoryWriteOutcome.ValidationFailed, "Inactive category slug remains reserved");
    Check((await service.EditAsync(other,"Duplicate inactive edit",prefix)).Outcome==CategoryWriteOutcome.ValidationFailed, "Inactive slug also blocks editing another category");
    var edited = await service.EditAsync(deactivated.Current!,"  QA edited category  ","  "+prefix.ToUpperInvariant()+"  ");
    Check(edited.Outcome==CategoryWriteOutcome.Succeeded && edited.Current!.Name=="QA edited category" && edited.Current.Slug==prefix && !edited.Current.IsActive, "Own slug is permitted; normalized edit preserves activation");
    Check((await service.EditAsync(deactivated.Current!,"Stale",prefix)).Outcome==CategoryWriteOutcome.Conflict, "Stale edit has an explicit conflict outcome");
    Check((await service.ChangeStatusAsync(deactivated.Current!)).Outcome==CategoryWriteOutcome.Conflict, "Changed details invalidate a status confirmation");
    var latest=edited.Current!;
    var token=service.Sign(latest);
    Check(service.Read(token,latest.Id)==latest && service.Read(token,Guid.NewGuid()) is null && service.Read("invalid-token",latest.Id) is null, "Signed snapshots retain existing purpose, identity binding and tamper rejection");
    var missing=latest with { Id=Guid.NewGuid() };
    Check((await service.EditAsync(missing,"Valid",prefix+"-missing")).Outcome==CategoryWriteOutcome.NotFound && (await service.ChangeStatusAsync(missing)).Outcome==CategoryWriteOutcome.NotFound, "Missing categories return explicit not-found outcomes");
    var product=new Product { Name="QA category reference",Slug=prefix+"-product",CategoryId=latest.Id };productId=product.Id;db.Products.Add(product);await db.SaveChangesAsync();
    Check((await service.ChangeStatusAsync(latest)).Outcome==CategoryWriteOutcome.Conflict, "Changed linked-product count requires confirmation review");
    latest=(await service.FindAsync(latest.Id))!;
    Check((await service.ChangeStatusAsync(latest)).Outcome==CategoryWriteOutcome.Succeeded && await db.Products.AnyAsync(p=>p.Id==product.Id && p.CategoryId==latest.Id && p.IsActive), "Activation preserves linked products and their flags");
    var maximumSlug=prefix+new string('x',160-prefix.Length);
    var boundary=await Create(new string('x',100),maximumSlug);
    Check(boundary.Name.Length==100 && boundary.Slug.Length==160, "Exact maximum name/slug lengths accepted without truncation");
    race.CreateSlug=prefix+"-create-race";
    var racedCreate=await service.CreateAsync("QA create race",race.CreateSlug);
    Check(racedCreate.Outcome==CategoryWriteOutcome.ValidationFailed && racedCreate.Errors.Single().Field=="Slug", "SQL unique constraint race returns creation validation outcome");
    Check(!db.ChangeTracker.Entries<Category>().Any(e=>e.State==EntityState.Added), "Failed unique insert detached; context can be reused");
    race.EditSlug=prefix+"-edit-race";
    var racedEdit=await service.EditAsync(other,"QA edit race",race.EditSlug);
    Check(racedEdit.Outcome==CategoryWriteOutcome.ValidationFailed && racedEdit.Errors.Single().Field=="Slug" && await service.FindAsync(other.Id)==other, "SQL unique constraint race returns edit validation without partial change");
    await Create("QA after race",prefix+"-after-race");
    for(var i=0;i<21;i++) await Create("QA page "+i.ToString("D2"),prefix+"-page-"+i.ToString("D2"));
    var page=await service.ListAsync("  "+prefix+"-page-  ","active",2,10);
    Check(page.Search==prefix+"-page-" && page.Total==21 && page.Page==2 && page.Pages==3 && page.Items.Count==10 && page.Items[0].Name=="QA page 10", "Focused listing owns search normalization, status and deterministic pagination");
    foreach(var size in new[] {10,25,50,100}) Check((await service.ListAsync(prefix+"-page-",pageSize:size)).PageSize==size, "Supported page size "+size);
    var last=await service.ListAsync(prefix+"-page-","unknown",999,999);
    Check(last.Status=="all" && last.PageSize==10 && last.Page==3 && last.Items.Count==1, "Listing allowlists and high-page clamp preserved");
    var first=await service.ListAsync(prefix+"-page-",page:-1);
    Check(first.Page==1 && first.Items.Count==10, "Low page clamps to one");
    var empty=await service.ListAsync(prefix+"-no-match","inactive");
    Check(empty.Total==0 && empty.Page==1 && empty.Pages==1 && empty.Items.Count==0, "Empty filtered page contract");
    Check((await service.ListAsync(new string('x',101))).Search.Length==100, "Search length bounded");
    Check(!typeof(CategoryManagement).GetProperties().Any(p=>typeof(IQueryable).IsAssignableFrom(p.PropertyType)), "No public IQueryable exposure");
    Console.WriteLine("Category direct-service suite passed.");
}
finally
{
    race.CreateSlug=race.EditSlug=null;db.ChangeTracker.Clear();
    if(productId.HasValue) await db.Products.Where(p=>p.Id==productId).ExecuteDeleteAsync();
    await db.Categories.Where(c=>ids.Contains(c.Id) || c.Slug.StartsWith(prefix)).ExecuteDeleteAsync();
    Console.WriteLine("Removed only this run's category/product fixtures.");
}
sealed class SlugRace(string connection) : SaveChangesInterceptor
{
    public string? CreateSlug;public string? EditSlug;
    public async Task Insert(string slug,CancellationToken ct)
    {
        await using var other=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
        other.Categories.Add(new Category { Name="QA concurrent slug",Slug=slug });await other.SaveChangesAsync(ct);
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {
        if(CreateSlug is string slug) { CreateSlug=null;await Insert(slug,ct); }return result;
    }
}
sealed class EditSlugRace(SlugRace race) : DbCommandInterceptor
{
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {
        if(race.EditSlug is string slug && command.CommandText.TrimStart().StartsWith("UPDATE",StringComparison.OrdinalIgnoreCase)) { race.EditSlug=null;await race.Insert(slug,ct); }
        return result;
    }
}
