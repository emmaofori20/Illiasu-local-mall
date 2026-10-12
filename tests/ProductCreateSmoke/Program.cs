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
builder.Logging.ClearProviders(); var counter=new QueryCounter();var rollback=new RollbackFault();
builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")).AddInterceptors(counter,rollback));
builder.Services.AddIdentity<ApplicationUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<ProductCreation>();
await using var app=builder.Build(); await using var scope=app.Services.CreateAsyncScope();
var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var service=scope.ServiceProvider.GetRequiredService<ProductCreation>();
var prefix="product-create-smoke-"+Guid.NewGuid().ToString("N")[..16]; var emails=new List<string>();
var categoryIds=new List<Guid>(); var productIds=new List<Guid>(); Guid? unitId=null;var unitIds=new List<Guid>();
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
    var category=new Category { Name="QA creation pantry",Slug=prefix };
    var unit=new UnitOfMeasure { Code="qa-"+Guid.NewGuid().ToString("N")[..12],Name="QA individual",Symbol="ea" };
    var pack=new UnitOfMeasure { Code="qa-"+Guid.NewGuid().ToString("N")[..12],Name="QA carton",Symbol="ct",Kind=UnitOfMeasureKind.Carton };
    var kg=new UnitOfMeasure { Code="qa-"+Guid.NewGuid().ToString("N")[..12],Name="QA kilogram",Symbol="kg",Kind=UnitOfMeasureKind.Mass,DecimalPlaces=3 };
    db.Categories.Add(category);categoryIds.Add(category.Id);db.UnitsOfMeasure.AddRange(unit,pack,kg);unitId=unit.Id;unitIds.AddRange([unit.Id,pack.Id,kg.Id]);
    await db.SaveChangesAsync();db.ChangeTracker.Clear();
    ProductCreateInput Input(string suffix)=>new() { Name="QA created product "+prefix+suffix,Slug=prefix+suffix,CategoryId=category.Id,Description="QA description",Variants=[new() { Name="QA each",Sku=prefix+suffix,UnitOfMeasureId=unit.Id,Price=12.50m }] };
    async Task<Guid> Save(ProductCreateInput input) { var result=await service.CreateAsync(input);if(!result.Id.HasValue) Console.WriteLine(string.Join("; ",result.Errors.Select(e=>e.Field+": "+e.Message)));Check(result.Id.HasValue,"Valid product creation: "+input.Variants.Count+" variants");productIds.Add(result.Id!.Value);db.ChangeTracker.Clear();return result.Id.Value; }
    var one=await Save(Input("-one"));
    var multi=Input("-multi");multi.Variants[0].Barcode="001234"+Guid.NewGuid().ToString("N")[..8];multi.Variants[0].Conversions.Add(new() { FromUnitOfMeasureId=pack.Id,Factor=12 });
    multi.Variants.Add(new() { Name="QA carton SKU",Sku=prefix+"-carton",UnitOfMeasureId=pack.Id,Price=140,IsActive=false,IsAvailable=false });
    var many=await Save(multi);
    Check(await db.ProductVariants.CountAsync(x=>x.ProductId==many)==2 && await db.ProductUnitConversions.AnyAsync(x=>x.ProductId==many && x.ToUnitOfMeasureId==unit.Id && x.Factor==12),"Multiple variants and explicit same-SKU conversion persisted");
    Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductId==one && x.ProductVariantId==x.StockVariantId && x.UnitsPerSale==1), "Single item uses one independent stock unit");
    var water = Input("-water"); var singleBottle = water.Variants[0]; singleBottle.Name="Single 500 ml bottle";
    var sharedCarton = new VariantCreateInput { Name="Carton of 12 from bottle stock", Sku=prefix+"-shared-carton", UnitOfMeasureId=pack.Id, Price=110,
        StockMode=StockBehaviour.Shared, StockOwnerRowKey=singleBottle.ClientRowKey, UnitsPerSale=12 };
    var sealedOption = new VariantCreateInput { Name="Sealed carton counted separately", Sku=prefix+"-sealed-carton", UnitOfMeasureId=pack.Id, Price=121, UnitsPerSale=99 };
    // Owner comes last: association must use its stable key, not its position.
    water.Variants=[sealedOption,sharedCarton,singleBottle]; var waterId=await Save(water);
    var waterRows=await db.ProductVariants.AsNoTracking().Where(x=>x.ProductId==waterId).ToDictionaryAsync(x=>x.Sku);
    var bottleId=waterRows[singleBottle.Sku].Id;var sharedId=waterRows[sharedCarton.Sku].Id;var sealedId=waterRows[sealedOption.Sku].Id;
    Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductVariantId==sharedId && x.StockVariantId==bottleId && x.UnitsPerSale==12 && x.BaseUnitOfMeasureId==unit.Id && x.SoldUnitOfMeasureId==pack.Id),"Reordered shared carton consumes 12 units of its keyed bottle source");
    Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductVariantId==sealedId && x.StockVariantId==sealedId && x.UnitsPerSale==1) && waterRows[sharedCarton.Sku].Price==110,"Sealed carton stays separate; self factor is server-derived and carton price independent");
    var rice=Input("-rice");rice.Variants[0].Name="5 kg sealed bag";rice.Variants.Add(new() { Name="25 kg sealed bag",Sku=prefix+"-rice25",UnitOfMeasureId=unit.Id,Price=300 });var riceId=await Save(rice);
    Check(await db.VariantStockMappings.CountAsync(x=>x.ProductId==riceId && x.StockVariantId==x.ProductVariantId && x.UnitsPerSale==1)==2,"5 kg and 25 kg rice bags remain independent");
    var measured=Input("-measured");measured.Variants[0].Name="Loose rice by kg";measured.Variants[0].UnitOfMeasureId=kg.Id;measured.Variants[0].AllowFractionalQuantity=true;measured.Variants[0].QuantityStep=.001m;
    measured.Variants.Add(new() { Name="250 g portion",Sku=prefix+"-portion",UnitOfMeasureId=unit.Id,Price=8,StockMode=StockBehaviour.Shared,StockOwnerRowKey=measured.Variants[0].ClientRowKey,UnitsPerSale=.25m });var measuredId=await Save(measured);
    Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductId==measuredId && x.StockVariantId!=x.ProductVariantId && x.UnitsPerSale==.25m && x.BaseUnitOfMeasureId==kg.Id),"Measured stock consumption respects kg precision");
    async Task Reject(ProductCreateInput input,string field,string label) {
        var count=await db.Products.CountAsync();var result=await service.CreateAsync(input);Check(!result.Id.HasValue && result.Errors.Any(x=>x.Field.EndsWith(field)) && await db.Products.CountAsync()==count,label);
    }
    var duplicate=Input("-duplicate");duplicate.Variants[0].Sku=prefix+"-one";await Reject(duplicate,"Sku","Existing SKU rejected");
    duplicate=Input("-barcode");duplicate.Variants[0].Barcode=multi.Variants[0].Barcode;await Reject(duplicate,"Barcode","Duplicate barcode rejected");
    duplicate=Input("-inactive");duplicate.Variants[0].Sku=prefix+"-carton";await Reject(duplicate,"Sku","Inactive SKU stays reserved");
    var invalid=Input("-negative");invalid.Variants[0].Price=-1;await Reject(invalid,"Price","Negative pricing rejected");
    invalid=Input("-precision");invalid.Variants[0].Price=1.123m;await Reject(invalid,"Price","Excess price precision rejected");
    invalid=Input("-missing-cat");invalid.CategoryId=Guid.NewGuid();await Reject(invalid,"CategoryId","Missing category rejected");
    invalid=Input("-missing-unit");invalid.Variants[0].UnitOfMeasureId=Guid.NewGuid();await Reject(invalid,"UnitOfMeasureId","Missing canonical unit rejected");
    invalid=Input("-conversion");invalid.Variants[0].Conversions.Add(new() { FromUnitOfMeasureId=pack.Id,Factor=0 });await Reject(invalid,"Factor","Nonpositive conversion rejected");
    invalid=Input("-sameunit");invalid.Variants[0].Conversions.Add(new() { FromUnitOfMeasureId=unit.Id,Factor=12 });await Reject(invalid,"FromUnitOfMeasureId","Self conversion rejected");
    invalid=Input("-fraction");invalid.Variants[0].AllowFractionalQuantity=true;invalid.Variants[0].QuantityStep=.5m;await Reject(invalid,"AllowFractionalQuantity","Fractional individual unit rejected");
    invalid=Input("-partial");invalid.Variants.Add(new() { Name="QA invalid",Sku=prefix+"-one",UnitOfMeasureId=unit.Id,Price=10 });await Reject(invalid,"Sku","Invalid second variant leaves no partial product");
    ProductCreateInput SharedInput(string suffix) { var input=Input(suffix); input.Variants.Add(new() { Name="Carton",Sku=prefix+suffix+"-carton",UnitOfMeasureId=pack.Id,Price=100,StockMode=StockBehaviour.Shared,StockOwnerRowKey=input.Variants[0].ClientRowKey,UnitsPerSale=12 });return input; }
    invalid=SharedInput("-missing-owner");invalid.Variants[1].StockOwnerRowKey=Guid.NewGuid();await Reject(invalid,"StockOwnerRowKey","Unknown/cross-product stock source rejected without writes");
    invalid=SharedInput("-self-owner");invalid.Variants[1].StockOwnerRowKey=invalid.Variants[1].ClientRowKey;await Reject(invalid,"StockOwnerRowKey","Shared option cannot point to itself");
    invalid=SharedInput("-missing-factor");invalid.Variants[1].UnitsPerSale=null;await Reject(invalid,"UnitsPerSale","Shared consumption must be explicit");
    invalid=SharedInput("-zero-factor");invalid.Variants[1].UnitsPerSale=0;await Reject(invalid,"UnitsPerSale","Zero shared consumption rejected");
    invalid=SharedInput("-negative-factor");invalid.Variants[1].UnitsPerSale=-12;await Reject(invalid,"UnitsPerSale","Negative shared consumption rejected");
    invalid=SharedInput("-fraction-factor");invalid.Variants[1].UnitsPerSale=.5m;await Reject(invalid,"UnitsPerSale","Whole-item source rejects fractional pack quantity");
    invalid=SharedInput("-precise-factor");invalid.Variants[1].UnitsPerSale=12.1234567m;await Reject(invalid,"UnitsPerSale","Shared quantity cannot silently round");
    invalid=SharedInput("-duplicate-key");invalid.Variants[1].ClientRowKey=invalid.Variants[0].ClientRowKey;await Reject(invalid,"ClientRowKey","Duplicate temporary row keys rejected");
    invalid=SharedInput("-chain");invalid.Variants.Add(new() { Name="Chained pack",Sku=prefix+"-chain-third",UnitOfMeasureId=pack.Id,Price=150,StockMode=StockBehaviour.Shared,StockOwnerRowKey=invalid.Variants[1].ClientRowKey,UnitsPerSale=2 });await Reject(invalid,"StockOwnerRowKey","Chained ownership rejected");
    invalid=SharedInput("-cycle");invalid.Variants[0].StockMode=StockBehaviour.Shared;invalid.Variants[0].StockOwnerRowKey=invalid.Variants[1].ClientRowKey;invalid.Variants[0].UnitsPerSale=1;await Reject(invalid,"StockOwnerRowKey","Cyclic ownership rejected");
    invalid=SharedInput("-mode");invalid.Variants[1].StockMode=(StockBehaviour)77;await Reject(invalid,"StockMode","Unknown stock behaviour rejected");
    invalid=SharedInput("-base-precision");invalid.Variants[0].UnitOfMeasureId=kg.Id;invalid.Variants[0].AllowFractionalQuantity=true;invalid.Variants[1].UnitsPerSale=.1234m;await Reject(invalid,"UnitsPerSale","Measured source's declared precision remains authoritative");
    invalid=SharedInput("-normalized-unit");invalid.Variants[0].UnitOfMeasureId=kg.Id;invalid.Variants[0].AllowFractionalQuantity=true;invalid.Variants[1].UnitsPerSale=.001m;invalid.Variants[1].UnitOfMeasureId=kg.Id;invalid.Variants[1].AllowFractionalQuantity=true;invalid.Variants[1].Conversions.Add(new() { FromUnitOfMeasureId=pack.Id,Factor=.001m });await Reject(invalid,"Factor","Receiving conversion must normalize to valid base-stock precision");
    var fault=Input("-db-failure");rollback.Enabled=true;
    try { await service.CreateAsync(fault);throw new Exception("Expected database failure"); }
    catch(InvalidOperationException e) when(e.Message=="QA forced insertion failure") { }
    finally {rollback.Enabled=false;db.ChangeTracker.Clear();}
    Check(!await db.Products.AnyAsync(x=>x.Slug==fault.Slug),"Transaction rollback after forced database insertion failure");
    using var anonymous=Client();Check((await anonymous.GetAsync("/Admin/Products/Create")).Headers.Location?.ToString().Contains("/Account/Login")==true,"Anonymous create challenge");
    foreach(var role in new[]{AppRoles.Customer,AppRoles.SalesStaff}) { using var denied=await Login(role);Check((await denied.GetAsync("/Admin/Products/Create")).Headers.Location?.ToString().Contains("/Account/AccessDenied")==true,role+" creation denied");Check((await Post(denied,"/Admin/Products/Create",await denied.GetStringAsync("/Account/Login"),new())).Headers.Location?.ToString().Contains("/Account/AccessDenied")==true,role+" POST denied"); }
    using var manager=await Login(AppRoles.InventoryManager);using var admin=await Login(AppRoles.Admin);
    Check((await admin.GetAsync("/Admin/Products/Create")).StatusCode==HttpStatusCode.OK,"Admin create page");
    var html=await manager.GetStringAsync("/Admin/Products/Create");
    Check((await manager.PostAsync("/Admin/Products/Create",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"Create antiforgery enforced");
    var values=new Dictionary<string,string> { ["Name"]="QA HTTP product "+prefix,["Slug"]=prefix+"-http",["CategoryId"]=category.Id.ToString(),["IsActive"]="true",["Variants[0].Name"]="Each",["Variants[0].Sku"]=prefix+"-http",["Variants[0].UnitOfMeasureId"]=unit.Id.ToString(),["Variants[0].Price"]="10",["Variants[0].QuantityStep"]="1",["Variants[0].IsActive"]="true",["Variants[0].IsAvailable"]="true",["Variants[0].Conversions[0].FromUnitOfMeasureId"]=pack.Id.ToString(),["Variants[0].Conversions[0].Factor"]="12",["Variants[0].Conversions[0].IsActive"]="true",["Variants[0].Conversions[0].ProductVariantId"]=Guid.NewGuid().ToString(),["Variants[0].Conversions[0].ProductId"]=many.ToString() };
    values["Variants[0].ClientRowKey"]=Guid.NewGuid().ToString(); values["Variants[0].StockMode"]="Own";
    var response=await Post(manager,"/Admin/Products/Create",html,values);
    Check(response.StatusCode==HttpStatusCode.Redirect,"HTTP creation succeeds");
    var http=await db.Products.AsNoTracking().SingleAsync(x=>x.Slug==prefix+"-http");productIds.Add(http.Id);
    var movement=await db.ProductUnitConversions.AsNoTracking().SingleAsync(x=>x.ProductId==http.Id);
    Check(await db.ProductVariants.AnyAsync(x=>x.Id==movement.ProductVariantId && x.ProductId==http.Id && x.UnitOfMeasureId==movement.ToUnitOfMeasureId),"Foreign product/SKU ownership injection ignored; server derives conversion target");
    var sharedValues=new Dictionary<string,string>(values) { ["Name"]="QA shared HTTP "+prefix,["Slug"]=prefix+"-http-shared",["Variants[0].Sku"]=prefix+"-http-shared-bottle" };
    var sourceKey=Guid.NewGuid().ToString();var optionKey=Guid.NewGuid().ToString();sharedValues["Variants[0].ClientRowKey"]=sourceKey;
    sharedValues["Variants[1].ClientRowKey"]=optionKey;sharedValues["Variants[1].Name"]="HTTP shared carton";sharedValues["Variants[1].Sku"]=prefix+"-http-shared-carton";sharedValues["Variants[1].UnitOfMeasureId"]=pack.Id.ToString();sharedValues["Variants[1].Price"]="95";sharedValues["Variants[1].QuantityStep"]="1";sharedValues["Variants[1].StockMode"]="Shared";sharedValues["Variants[1].StockOwnerRowKey"]=sourceKey;sharedValues["Variants[1].UnitsPerSale"]="12";
    sharedValues["Variants[1].StockVariantId"]=sealedId.ToString();sharedValues["Variants[1].ProductId"]=waterId.ToString();
    Check((await Post(manager,"/Admin/Products/Create",html,sharedValues)).StatusCode==HttpStatusCode.Redirect,"HTTP shared-stock creation succeeds");
    var savedShared=await db.Products.AsNoTracking().SingleAsync(x=>x.Slug==prefix+"-http-shared");
    Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductId==savedShared.Id && x.StockVariantId!=x.ProductVariantId && x.UnitsPerSale==12 && x.StockVariantId!=sealedId),"HTTP derives same-product ownership and ignores injected persistent IDs");
    sharedValues["Slug"]=prefix+"-http-invalid-map";sharedValues["Variants[0].Sku"]=prefix+"-invalid-map-bottle";sharedValues["Variants[1].Sku"]=prefix+"-invalid-map-carton";sharedValues["Variants[1].UnitsPerSale"]="0";
    var mapResponse=await Post(manager,"/Admin/Products/Create",html,sharedValues);var mapHtml=await mapResponse.Content.ReadAsStringAsync();
    Check(mapResponse.StatusCode==HttpStatusCode.OK && mapHtml.Contains("field-validation-error") && mapHtml.Contains(sourceKey) && mapHtml.Contains(optionKey) && mapHtml.Contains("value=\"Shared\"") && !await db.Products.AnyAsync(x=>x.Slug==sharedValues["Slug"]),"HTTP mapping errors preserve keys/options and save no partial product");
    values["Slug"]=prefix+"-invalid-http";values["Variants[0].Price"]="not-a-price";
    response=await Post(manager,"/Admin/Products/Create",html,values);var errorHtml=await response.Content.ReadAsStringAsync();Check(errorHtml.Contains("validation-summary-errors") && errorHtml.Contains("QA HTTP product"),"Binding errors preserve entered fields");
    var boundary=new ILLiasu_Local_Mall.Infrastructure.Storage.DisabledProductImageUploadBoundary();
    var fake=new Microsoft.AspNetCore.Http.FormFile(new MemoryStream([1,2,3]),0,3,"Images","../../unsafe.png") { Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(),ContentType="image/png" };
    Check(boundary.Validate([fake],0).Any(x=>x.Contains("not enabled")),"Spoofed image and original path never accepted/stored");
    Console.WriteLine("Product creation integration suite passed.");
    if(args.Contains("--preview")) {
        var file=Path.Combine(Path.GetTempPath(),"illiasu-product-create-preview.json");File.Delete(file+".done");
        var cookies=jars[manager].GetCookies(new Uri("https://localhost:7116")).Cast<Cookie>().Select(x=>new { name=x.Name,value=x.Value,domain=x.Domain,path=x.Path,secure=x.Secure,httpOnly=x.HttpOnly });
        await File.WriteAllTextAsync(file,System.Text.Json.JsonSerializer.Serialize(new { cookies,query=prefix,categoryId=category.Id,unitId=unit.Id,packId=pack.Id }));
        Console.WriteLine("Creation preview ready with temporary fixtures.");var until=DateTime.UtcNow.AddMinutes(10);while(!File.Exists(file+".done") && DateTime.UtcNow<until)await Task.Delay(500);
        if(File.Exists(file+".done")) {
            var browserProduct=await db.Products.AsNoTracking().SingleAsync(x=>x.Slug==prefix+"-browser");
            var browserRows=await db.ProductVariants.AsNoTracking().Where(x=>x.ProductId==browserProduct.Id).ToDictionaryAsync(x=>x.Sku);
            var ownerId=browserRows[prefix+"-browser-a"].Id;var cartonId=browserRows[prefix+"-browser-b"].Id;var sealedCartonId=browserRows[prefix+"-browser-c"].Id;
            Check(await db.VariantStockMappings.AnyAsync(x=>x.ProductVariantId==cartonId && x.StockVariantId==ownerId && x.UnitsPerSale==12) && await db.VariantStockMappings.AnyAsync(x=>x.ProductVariantId==sealedCartonId && x.StockVariantId==sealedCartonId && x.UnitsPerSale==1) && browserRows[prefix+"-browser-b"].Price==110,"Browser save persisted shared/sealed mappings and independently entered prices");
        }
        File.Delete(file);File.Delete(file+".done");
    }

} finally {
    productIds=await db.Products.Where(x=>x.Slug.StartsWith(prefix)).Select(x=>x.Id).ToListAsync();
    await db.ProductUnitConversions.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.ProductImages.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.VariantStockMappings.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.ProductVariants.Where(x=>productIds.Contains(x.ProductId)).ExecuteDeleteAsync();
    await db.Products.Where(x=>productIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.Categories.Where(x=>categoryIds.Contains(x.Id)).ExecuteDeleteAsync();
    await db.UnitsOfMeasure.Where(x=>unitIds.Contains(x.Id)).ExecuteDeleteAsync();
    foreach(var email in emails) { var user=await users.FindByEmailAsync(email);if(user is not null) await users.DeleteAsync(user); }
    Console.WriteLine("Removed only this run's product/category/unit/account fixtures.");
}
sealed class QueryCounter : DbCommandInterceptor {
    public int Count;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken ct=default) { Count++;return ValueTask.FromResult(result); }
}

sealed class RollbackFault : SaveChangesInterceptor {
    public bool Enabled;
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default) {
        if(Enabled) throw new InvalidOperationException("QA forced insertion failure");return ValueTask.FromResult(result);
    }
}
