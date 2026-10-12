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
builder.Logging.ClearProviders(); var rollback=new EditFault();
builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")).AddInterceptors(rollback));
builder.Services.AddIdentity<ApplicationUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<ProductCreation>();
builder.Services.AddScoped<ProductEditing>();
await using var app=builder.Build(); await using var scope=app.Services.CreateAsyncScope();
var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var creation=scope.ServiceProvider.GetRequiredService<ProductCreation>();
var service=scope.ServiceProvider.GetRequiredService<ProductEditing>();
var prefix="product-edit-smoke-"+Guid.NewGuid().ToString("N")[..16]; var emails=new List<string>();
var categoryIds=new List<Guid>(); var productIds=new List<Guid>();
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
async Task<ProductEditInput> Load(Guid id)
{
    db.ChangeTracker.Clear();
    var p=await db.Products.AsNoTracking().Include(x=>x.Variants).SingleAsync(x=>x.Id==id);
    return new() { Id=id,Version=Convert.ToBase64String(p.RowVersion),Name=p.Name,Slug=p.Slug,CategoryId=p.CategoryId,Description=p.Description,IsActive=p.IsActive,
        ExistingVariants=p.Variants.OrderBy(x=>x.Name).ThenBy(x=>x.Id).Select(v=>new ExistingVariantEdit { Id=v.Id,Version=Convert.ToBase64String(v.RowVersion),Price=v.Price,Barcode=v.Barcode,IsActive=v.IsActive,IsAvailable=v.IsAvailable }).ToList() };
}
Dictionary<string,string> Values(ProductEditInput input)
{
    var values=new Dictionary<string,string> { ["Id"]=input.Id.ToString(),["Version"]=input.Version,["Name"]=input.Name,["CategoryId"]=input.CategoryId.ToString(),["Description"]=input.Description??"",["IsActive"]=input.IsActive.ToString() };
    for(var i=0;i<input.ExistingVariants.Count;i++) {
        var v=input.ExistingVariants[i];var k=$"ExistingVariants[{i}].";
        values[k+"Id"]=v.Id.ToString();values[k+"Version"]=v.Version;values[k+"Price"]=v.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values[k+"Barcode"]=v.Barcode??"";values[k+"IsActive"]=v.IsActive.ToString();values[k+"IsAvailable"]=v.IsAvailable.ToString();
    }
    return values;
}
async Task<string> Rules(Guid id) => System.Text.Json.JsonSerializer.Serialize(await db.ProductVariants.AsNoTracking().Where(v=>v.ProductId==id).OrderBy(v=>v.Id)
    .Select(v=>new { v.Id,v.Name,v.Sku,v.UnitOfMeasureId,v.QuantityStep,v.AllowFractionalQuantity }).ToListAsync()) +
    System.Text.Json.JsonSerializer.Serialize(await db.VariantStockMappings.AsNoTracking().Where(m=>m.ProductId==id).OrderBy(m=>m.ProductVariantId).ToListAsync());
try {
    var category=new Category { Name="QA edit category "+prefix,Slug=prefix };categoryIds.Add(category.Id);
    db.Categories.Add(category);await db.SaveChangesAsync();
    var unit=await db.UnitsOfMeasure.AsNoTracking().SingleAsync(u=>u.Code=="PCS" && u.IsActive);
    var carton=await db.UnitsOfMeasure.AsNoTracking().SingleAsync(u=>u.Code=="CTN" && u.IsActive);
    var own=new VariantCreateInput { Name="Single bottle",Sku=prefix+"-bottle",UnitOfMeasureId=unit.Id,Price=10 };
    var shared=new VariantCreateInput { Name="Carton from bottles",Sku=prefix+"-carton",UnitOfMeasureId=carton.Id,Price=110,StockMode=StockBehaviour.Shared,StockOwnerRowKey=own.ClientRowKey,UnitsPerSale=12 };
    var sealedOption=new VariantCreateInput { Name="Sealed carton",Sku=prefix+"-sealed",UnitOfMeasureId=carton.Id,Price=125 };
    var created=await creation.CreateAsync(new() { Name="QA water "+prefix,Slug=prefix,CategoryId=category.Id,Variants=[own,shared,sealedOption] });
    Check(created.Id.HasValue,"Edit fixture created with independent/shared/sealed options");var id=created.Id!.Value;productIds.Add(id);
    var baseline=await Rules(id);var edit=await Load(id);var stale=await Load(id);var originalVersion=edit.Version;
    edit.Name="Updated water "+prefix;edit.Description="Updated description";edit.IsActive=false;edit.ExistingVariants[0].Price=117.25m;edit.ExistingVariants[0].Barcode=prefix+"-barcode";edit.ExistingVariants[1].IsAvailable=false;edit.ExistingVariants[2].IsActive=false;
    Check((await service.EditAsync(edit)).Id==id,"Product details, prices, barcode, activation and manual availability saved");
    var updated=await Load(id);Check(updated.Name==edit.Name && updated.Description==edit.Description && !updated.IsActive && updated.Version!=originalVersion,"Product data and aggregate rowversion updated");
    Check(updated.ExistingVariants[0].Price==117.25m && updated.ExistingVariants[0].Barcode==prefix+"-barcode" && !updated.ExistingVariants[1].IsAvailable && !updated.ExistingVariants[2].IsActive,"Mutable selling-option changes persisted");
    Check(await Rules(id)==baseline,"All existing identities, units, increments and stock mappings preserved");
    Check(!(await service.EditAsync(stale)).Id.HasValue,"Stale product rowversion rejected");
    var duplicate=await Load(id);duplicate.ExistingVariants[1].Barcode=duplicate.ExistingVariants[0].Barcode;duplicate.Name="Must not save";
    Check((await service.EditAsync(duplicate)).Errors.Any(e=>e.Field.EndsWith(".Barcode")),"Duplicate barcode produces field validation");
    Check((await Load(id)).Name==updated.Name,"Validation error rolls back entire edit");
    db.ChangeTracker.Clear();
    var other=await creation.CreateAsync(new() { Name="Other water "+prefix,Slug=prefix+"-other",CategoryId=category.Id,
        Variants=[new() { Name="Other item",Sku=prefix+"-other",Barcode=prefix+"-reserved",UnitOfMeasureId=unit.Id,Price=3 }] });
    Check(other.Id.HasValue,"Foreign product uniqueness fixture created");productIds.Add(other.Id!.Value);
    var globalBarcode=await Load(id);globalBarcode.ExistingVariants[0].Barcode=prefix+"-reserved";
    Check((await service.EditAsync(globalBarcode)).Errors.Any(e=>e.Field.EndsWith(".Barcode")),"Barcode belonging to another product rejected");
    var duplicateSku=await Load(id);duplicateSku.Variants.Add(new() { Name="Duplicate option",Sku=own.Sku,UnitOfMeasureId=unit.Id,Price=1 });
    Check((await service.EditAsync(duplicateSku)).Errors.Any(e=>e.Field=="Variants[0].Sku"),"New option cannot reuse existing SKU");
    var crossProduct=await Load(id);var otherRoot=await db.ProductVariants.AsNoTracking().Where(v=>v.ProductId==other.Id).Select(v=>v.Id).SingleAsync();
    crossProduct.Variants.Add(new() { Name="Foreign sharing",Sku=prefix+"-foreign-share",UnitOfMeasureId=carton.Id,Price=1,StockMode=StockBehaviour.Shared,StockOwnerRowKey=otherRoot,UnitsPerSale=12 });
    Check((await service.EditAsync(crossProduct)).Errors.Any(e=>e.Field=="Variants[0].StockOwnerRowKey"),"New option cannot consume another product's stock");
    var staleVariant=await Load(id);var variantId=staleVariant.ExistingVariants[0].Id;
    await db.ProductVariants.Where(v=>v.Id==variantId).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.Price,v=>v.Price+1));
    Check((await service.EditAsync(staleVariant)).Errors.Any(e=>e.Field.EndsWith(".Version")),"Stale selling-option rowversion rejected even without product token change");
    var malformed=await Load(id);malformed.Version="not base64";Check(!(await service.EditAsync(malformed)).Id.HasValue,"Malformed rowversion rejected");
    var missing=await Load(id);missing.ExistingVariants.RemoveAt(0);Check(!(await service.EditAsync(missing)).Id.HasValue,"Existing option removal rejected");
    var foreign=await Load(id);foreign.ExistingVariants[0].Id=Guid.NewGuid();Check(!(await service.EditAsync(foreign)).Id.HasValue,"Foreign option identity rejected");
    var withNew=await Load(id);var rootId=await db.ProductVariants.AsNoTracking().Where(v=>v.ProductId==id && v.Sku==own.Sku).Select(v=>v.Id).SingleAsync();
    withNew.Variants.Add(new() { Name="Pack of six",Sku=prefix+"-pack6",UnitOfMeasureId=carton.Id,Price=53,StockMode=StockBehaviour.Shared,StockOwnerRowKey=rootId,UnitsPerSale=6 });
    Check((await service.EditAsync(withNew)).Id==id,"New shared option saved against existing same-product root");
    var newVariant=await db.ProductVariants.AsNoTracking().SingleAsync(v=>v.Sku==prefix+"-pack6");
    Check(await db.VariantStockMappings.AnyAsync(m=>m.ProductVariantId==newVariant.Id && m.StockVariantId==rootId && m.UnitsPerSale==6),"New shared mapping correctly persisted");
    var chain=await Load(id);chain.Variants.Add(new() { Name="Invalid chain",Sku=prefix+"-chain",UnitOfMeasureId=carton.Id,Price=1,StockMode=StockBehaviour.Shared,StockOwnerRowKey=newVariant.Id,UnitsPerSale=2 });
    Check((await service.EditAsync(chain)).Errors.Any(e=>e.Field=="Variants[0].StockOwnerRowKey"),"Chained ownership rejected with correctly indexed field error");
    var newPair=await Load(id);var independent=new VariantCreateInput { Name="New independent bottle",Sku=prefix+"-new-root",UnitOfMeasureId=unit.Id,Price=12 };
    newPair.Variants=[new() { Name="New root shared carton",Sku=prefix+"-new-share",UnitOfMeasureId=carton.Id,Price=120,StockMode=StockBehaviour.Shared,StockOwnerRowKey=independent.ClientRowKey,UnitsPerSale=12 },independent];
    Check((await service.EditAsync(newPair)).Id==id,"New root and reordered shared option saved atomically");
    var beforeFault=await Load(id);var fault=await Load(id);fault.Name="Rollback edited name";fault.ExistingVariants[0].Price=999;fault.Variants.Add(new() { Name="Rollback option",Sku=prefix+"-rollback",UnitOfMeasureId=unit.Id,Price=1 });
    rollback.Enabled=true;
    try { await service.EditAsync(fault);throw new Exception("Expected forced failure"); } catch(InvalidOperationException e) when(e.Message=="QA forced post-save failure") { Console.WriteLine("PASS: Forced post-save failure"); } finally { rollback.Enabled=false;db.ChangeTracker.Clear(); }
    var afterFault=await Load(id);Check(afterFault.Name==beforeFault.Name && afterFault.Version==beforeFault.Version && afterFault.ExistingVariants[0].Price==beforeFault.ExistingVariants[0].Price && !await db.ProductVariants.AnyAsync(v=>v.Sku==prefix+"-rollback"),"Post-save failure rolls back product, variants, rowversions and new mapping");
    rollback.ConcurrentId=id;var race=await Load(id);race.Name="Concurrent edit";
    Check(!(await service.EditAsync(race)).Id.HasValue,"EF rowversion predicate catches a change during SaveChanges");rollback.ConcurrentId=null;
    Check((await Load(id)).Version==afterFault.Version,"Concurrency failure rolls back the transaction");
    var url="/Admin/Products/Edit/"+id;var anonymous=Client();Check((await anonymous.GetAsync(url)).StatusCode==HttpStatusCode.Redirect,"Anonymous edit denied");
    foreach(var role in new[] { AppRoles.Customer,AppRoles.SalesStaff }) {
        var denied=await Login(role);Check((await denied.GetAsync(url)).StatusCode==HttpStatusCode.Redirect,"Unauthorized "+role+" edit GET denied");
        Check((await denied.PostAsync(url,new FormUrlEncodedContent(Values(await Load(id))))).StatusCode==HttpStatusCode.Redirect,"Unauthorized "+role+" edit POST denied");
    }
    var manager=await Login(AppRoles.InventoryManager);var admin=await Login(AppRoles.Admin);
    Check((await admin.GetAsync(url)).StatusCode==HttpStatusCode.OK,"Admin edit GET succeeds");
    var html=await manager.GetStringAsync(url);Check(html.Contains("Stock behaviour (read-only)") && html.Contains("Save changes"),"Dedicated edit page renders existing read-only mappings");
    Check((await manager.PostAsync(url,new FormUrlEncodedContent(Values(await Load(id))))).StatusCode==HttpStatusCode.BadRequest,"Edit antiforgery enforced");
    var posted=await Load(id);posted.Name="HTTP edited water "+prefix;var values=Values(posted);
    values["Slug"]="tampered-url";values["ExistingVariants[0].Sku"]="tampered-sku";values["ExistingVariants[0].UnitOfMeasureId"]=carton.Id.ToString();values["ExistingVariants[0].StockVariantId"]=Guid.NewGuid().ToString();values["ExistingVariants[0].UnitsPerSale"]="99";
    var rulesBeforeHttp=await Rules(id);Check((await Post(manager,url,html,values)).StatusCode==HttpStatusCode.Redirect,"InventoryManager HTTP edit succeeds");
    Check(await Rules(id)==rulesBeforeHttp && (await Load(id)).Slug==prefix,"Immutable identity and stock mapping overposting ignored");
    html=await manager.GetStringAsync(url);posted=await Load(id);posted.ExistingVariants[1].Barcode=posted.ExistingVariants[0].Barcode;
    // Select a nonempty barcode from the fixture even if option ordering changed.
    var barcode=posted.ExistingVariants.First(v=>v.Barcode!=null).Barcode;posted.ExistingVariants.First(v=>v.Barcode==null).Barcode=barcode;
    var badResponse=await Post(manager,url,html,Values(posted));var badHtml=await badResponse.Content.ReadAsStringAsync();Check(badResponse.StatusCode==HttpStatusCode.OK && badHtml.Contains("Barcode is already used"),"HTTP duplicate barcode shows field error and retains entries");
    html=await manager.GetStringAsync(url);var staleHttp=await Load(id);var latest=await Load(id);latest.Name="Latest water "+prefix;Check((await service.EditAsync(latest)).Id==id,"Concurrent HTTP test writer saved");
    var staleResponse=await Post(manager,url,html,Values(staleHttp));Check((await staleResponse.Content.ReadAsStringAsync()).Contains("changed since you opened"),"HTTP stale page blocked with reload guidance");
    if(args.Contains("--preview")) {
        var file=Path.Combine(Path.GetTempPath(),"illiasu-product-edit-preview.json");if(File.Exists(file+".done"))File.Delete(file+".done");
        var cookies=jars[manager].GetCookies(new Uri("https://localhost:7116")).Cast<Cookie>().Select(x=>new { name=x.Name,value=x.Value,domain=x.Domain,path=x.Path,secure=x.Secure,httpOnly=x.HttpOnly });
        await File.WriteAllTextAsync(file,System.Text.Json.JsonSerializer.Serialize(new { cookies,query=prefix,id,rootId,unitId=unit.Id,packId=carton.Id,categoryId=category.Id }));
        Console.WriteLine("Edit preview ready with temporary fixtures.");var until=DateTime.UtcNow.AddMinutes(10);while(!File.Exists(file+".done") && DateTime.UtcNow<until)await Task.Delay(500);
        Check(File.Exists(file+".done"),"Browser preview completed");
        Check(await db.ProductVariants.AnyAsync(v=>v.ProductId==id && v.Sku==prefix+"-browser"),"Browser-created selling option persisted");File.Delete(file);File.Delete(file+".done");
    }
    Console.WriteLine("Product editing integration suite passed.");
}
finally {
    rollback.Enabled=false;rollback.ConcurrentId=null;db.ChangeTracker.Clear();
    await db.ProductUnitConversions.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.ProductImages.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.VariantStockMappings.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.ProductVariants.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.Products.Where(x=>productIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Categories.Where(x=>categoryIds.Contains(x.Id)).ExecuteDeleteAsync();
    foreach(var email in emails) { var user=await users.FindByEmailAsync(email);if(user is not null) await users.DeleteAsync(user); }
    Console.WriteLine("Removed only this run's edit fixtures and accounts; reference units preserved.");
}
sealed class EditFault : SaveChangesInterceptor {
    public bool Enabled;public Guid? ConcurrentId;
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default) {
        if(ConcurrentId is Guid id) { ConcurrentId=null;await data.Context!.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET Name = Name WHERE Id = {id}",ct); }
        return result;
    }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default) {
        if(Enabled) throw new InvalidOperationException("QA forced post-save failure");return ValueTask.FromResult(result);
    }
}
