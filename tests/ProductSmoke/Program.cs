using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ILLiasu_Local_Mall.Application.Catalog;
using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var builder=WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName=typeof(ApplicationDbContext).Assembly.GetName().Name,ContentRootPath=root,EnvironmentName="Development" });
IdentityProvisioning.RequireLocalDevelopment(builder.Environment,builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Logging.ClearProviders(); var counter=new QueryCounter();
builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")).AddInterceptors(counter));
builder.Services.AddIdentity<ApplicationUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<ProductManagement>();
await using var app=builder.Build(); await using var scope=app.Services.CreateAsyncScope();
var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var service=scope.ServiceProvider.GetRequiredService<ProductManagement>();
var prefix="product-smoke-"+Guid.NewGuid().ToString("N"); var emails=new List<string>();
var categoryIds=new List<Guid>(); var productIds=new List<Guid>(); Guid? unitId=null;
var jars=new Dictionary<HttpClient,CookieContainer>();
void Check(bool ok,string label) { if(!ok) throw new Exception("FAIL: "+label); Console.WriteLine("PASS: "+label); }
string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+name+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
HttpClient Client() { var jar=new CookieContainer();var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,CookieContainer=jar,ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress=new("https://localhost:7116") };jars[client]=jar;return client; }
async Task<HttpResponseMessage> Post(HttpClient c,string url,string html,Dictionary<string,string> values) { values["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken");return await c.PostAsync(url,new FormUrlEncodedContent(values)); }
async Task<HttpClient> Login(string role) {
    var email=prefix+role+"@example.invalid";emails.Add(email);var pwd="Aa1!"+Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var user=new ApplicationUser { UserName=email,Email=email };
    Check((await users.CreateAsync(user,pwd)).Succeeded && (await users.AddToRoleAsync(user,role)).Succeeded,"Temporary "+role+" fixture");
    var client=Client();Check((await Post(client,"/Account/Login",await client.GetStringAsync("/Account/Login"),new() { ["Email"]=email,["Password"]=pwd })).StatusCode==HttpStatusCode.Redirect,role+" login");return client;
}
try {
    var categories=new[] { new Category { Name="QA sample pantry",Slug=prefix+"-a" },new Category { Name="QA sample chilled",Slug=prefix+"-b",IsActive=false } };
    db.Categories.AddRange(categories);categoryIds.AddRange(categories.Select(x=>x.Id));
    var unit=new UnitOfMeasure { Code="qa-"+Guid.NewGuid().ToString("N")[..12],Name="QA sample unit",Symbol="ea" };db.UnitsOfMeasure.Add(unit);unitId=unit.Id;
    var products=Enumerable.Range(0,27).Select(n=>new Product { Name=$"QA sample product {n:D2} {prefix}",Slug=prefix+"-"+n,CategoryId=categories[n%2].Id,IsActive=n%3!=0 }).ToArray();
    db.Products.AddRange(products); productIds.AddRange(products.Select(x=>x.Id));
    var variants=products.Take(26).Select((p,n)=>new ProductVariant { ProductId=p.Id,Name="QA unit",Sku=prefix+"-sku-"+n,UnitOfMeasureId=unit.Id,Price=7.50m }).ToList();
    variants.Add(new ProductVariant { ProductId=products[1].Id,Name="QA second unit",Sku=prefix+"-extra",UnitOfMeasureId=unit.Id,Price=20m });
    variants.Add(new ProductVariant { ProductId=products[1].Id,Name="QA inactive unit",Sku=prefix+"-inactive",UnitOfMeasureId=unit.Id,Price=100m,IsActive=false });
    db.ProductVariants.AddRange(variants);
    db.ProductImages.Add(new ProductImage { ProductId=products[1].Id,Path="/images/samples/rice-thumb.jpg",AltText="QA sample grocery photo",IsPrimary=true });
    await db.SaveChangesAsync();db.ChangeTracker.Clear();counter.Count=0;
    var list=await service.ListAsync(prefix,null,"all",1,10);
    Check(list.Total==27 && list.Items.Count==10 && list.Pages==3,"SQL-backed counts and default pagination");
    Check(counter.Count==3 && db.ChangeTracker.Entries().Count()==0,"Three projection queries, no N+1 or tracking");
    var priced=list.Items.Single(x=>x.Id==products[1].Id);
    Check(priced.Variants==3 && priced.MinimumPrice==7.50m && priced.MaximumPrice==100m && priced.ImagePath=="/images/samples/rice-thumb.jpg","Variant count, price range and primary image projection");
    var emptyVariant=await service.ListAsync(products[26].Name,null,"all",1,10);
    Check(emptyVariant.Items.Single().Variants==0 && emptyVariant.Items.Single().MinimumPrice is null,"Products without variants have nullable prices");
    Check((await service.ListAsync(prefix+"-extra",null,"all",1,10)).Items.Single().Id==products[1].Id,"SKU search across variants");
    Check((await service.ListAsync(prefix.ToUpperInvariant(),null,"all",1,10)).Total==27,"Case-insensitive product-name search");
    var filtered=await service.ListAsync(prefix,categories[0].Id,"inactive",1,100);
    Check(filtered.Total==5 && filtered.Items.All(x=>!x.IsActive && x.Category==categories[0].Name),"Combined category and status filters");
    foreach(var size in new[]{10,25,50,100}) { var result=await service.ListAsync(prefix,null,"all",1,size);Check(result.Items.Count==Math.Min(size,27),"Page size "+size); }
    Check((await service.ListAsync(prefix,null,"all",999,10)).Page==3,"Out-of-range pages clamp to last page");
    var safe=await service.ListAsync(prefix,null,"invalid",-1,999);Check(safe.Page==1 && safe.PageSize==10 && safe.Status=="all","Safe page/status allowlists");
    Check((await service.ListAsync("' OR 1=1 --",null,"all",1,10)).Total==0,"Search remains a parameterized literal");
    Check(ProductManagement.SafeImagePath("//remote.invalid/a.jpg") is null && ProductManagement.SafeImagePath("javascript:alert(1)") is null,"Unsafe image paths excluded");
    using var anonymous=Client();Check((await anonymous.GetAsync("/Admin/Products")).Headers.Location?.ToString().Contains("/Account/Login")==true,"Anonymous product challenge");
    foreach(var role in new[]{AppRoles.Customer,AppRoles.SalesStaff}) {
        using var denied=await Login(role);
        foreach(var path in new[]{"/Admin/Products","/Admin/Products/Create","/Admin/Products/Edit/"+products[1].Id,"/Admin/Products/Status/"+products[1].Id})
            Check((await denied.GetAsync(path)).Headers.Location?.ToString().Contains("/Account/AccessDenied")==true,role+" denied product action");
        Check((await Post(denied,"/Admin/Products/Status/"+products[1].Id,await denied.GetStringAsync("/Account/Login"),new())).Headers.Location?.ToString().Contains("/Account/AccessDenied")==true,role+" cannot mutate status");
    }
    using var admin=await Login(AppRoles.Admin);Check((await admin.GetAsync("/Admin/Products")).StatusCode==HttpStatusCode.OK,"Admin listing access");
    using var manager=await Login(AppRoles.InventoryManager);Check((await manager.GetAsync("/Admin/Products")).StatusCode==HttpStatusCode.OK,"InventoryManager listing access");
    Check((await manager.GetAsync("/Admin")).Headers.Location?.ToString().Contains("/Account/AccessDenied")==true,"Other admin permissions remain restricted");
    Check((await manager.GetAsync("/Admin/Products?categoryId=not-a-guid")).StatusCode==HttpStatusCode.BadRequest,"Invalid category parameter rejected");
    Check((await manager.GetStringAsync("/Admin/Products/Create")).Contains("Save product") && (await manager.GetStringAsync("/Admin/Products/Edit/"+products[1].Id)).Contains("Save changes"),"Dedicated creation and editing workflows are available");
    var url="/Admin/Products/Status/"+products[1].Id;var html=await manager.GetStringAsync(url);
    Check((await manager.PostAsync(url,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"Status antiforgery enforced");
    Check((await Post(manager,url,html,new() { ["Snapshot"]="tampered",["Confirm"]="true" })).StatusCode==HttpStatusCode.BadRequest,"Tampered status snapshot rejected");
    var snapshot=Field(html,"Snapshot");
    Check((await Post(manager,url,html,new() { ["Snapshot"]=snapshot,["Confirm"]="false" })).StatusCode==HttpStatusCode.OK,"Explicit confirmation required");
    await db.Products.Where(x=>x.Id==products[1].Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Description,"QA concurrent edit"));
    Check((await Post(manager,url,html,new() { ["Snapshot"]=snapshot,["Confirm"]="true" })).StatusCode==HttpStatusCode.Conflict,"Rowversion rejects stale status changes");
    html=await manager.GetStringAsync(url);snapshot=Field(html,"Snapshot");
    Check((await Post(manager,url,html,new() { ["Snapshot"]=snapshot,["Confirm"]="true" })).StatusCode==HttpStatusCode.Redirect,"Confirmed product deactivation");
    Check((await Post(manager,url,html,new() { ["Snapshot"]=snapshot,["Confirm"]="true" })).StatusCode==HttpStatusCode.Redirect,"Duplicate status request idempotent");
    Check(!await db.Products.Where(x=>x.Id==products[1].Id).Select(x=>x.IsActive).SingleAsync() && await db.ProductVariants.CountAsync(x=>x.ProductId==products[1].Id)==3 && await db.ProductImages.AnyAsync(x=>x.ProductId==products[1].Id),"Deactivation preserves variants and images");
    Console.WriteLine("Product integration suite passed.");
    if(args.Contains("--preview")) {
        var file=Path.Combine(Path.GetTempPath(),"illiasu-product-preview.json");File.Delete(file+".done");
        var cookies=jars[manager].GetCookies(new Uri("https://localhost:7116")).Cast<Cookie>().Select(x=>new { name=x.Name,value=x.Value,domain=x.Domain,path=x.Path,secure=x.Secure,httpOnly=x.HttpOnly });
        await File.WriteAllTextAsync(file,System.Text.Json.JsonSerializer.Serialize(new { cookies,id=products[2].Id,query=prefix,categoryId=categories[0].Id }));
        Console.WriteLine("Temporary product preview ready; automatic cleanup after ten minutes.");var until=DateTime.UtcNow.AddMinutes(10);
        while(!File.Exists(file+".done") && DateTime.UtcNow<until) await Task.Delay(500);File.Delete(file);File.Delete(file+".done");
    }
} finally {
    await db.ProductImages.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.VariantStockMappings.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.ProductVariants.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.Products.Where(x=>productIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Categories.Where(x=>categoryIds.Contains(x.Id)).ExecuteDeleteAsync();
    if(unitId.HasValue) await db.UnitsOfMeasure.Where(x=>x.Id==unitId).ExecuteDeleteAsync();
    foreach(var email in emails) { var user=await users.FindByEmailAsync(email);if(user is not null) await users.DeleteAsync(user); }
    Console.WriteLine("Removed only this run's product/category/unit/account fixtures.");
}
sealed class QueryCounter : DbCommandInterceptor {
    public int Count;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken ct=default) { Count++;return ValueTask.FromResult(result); }
}
