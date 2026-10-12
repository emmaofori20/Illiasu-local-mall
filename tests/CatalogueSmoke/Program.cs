using ILLiasu_Local_Mall.Domain.Entities;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name,
    ContentRootPath = root, EnvironmentName = "Development"
});
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
void Check(bool ok, string name) { if(!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
var expected = new[]{"Categories","Products","ProductVariants","UnitsOfMeasure","ProductUnitConversions","ProductImages"};
var assembly = db.GetService<IMigrationsAssembly>();
var migration = assembly.CreateMigration(assembly.Migrations.Single(x => x.Key.EndsWith("_AddCatalogueFoundation")).Value, db.Database.ProviderName!);
Check(migration.UpOperations.All(o => o is CreateTableOperation t && expected.Contains(t.Name) || o is CreateIndexOperation i && expected.Contains(i.Table)), "Migration contains only catalogue create-table/index operations");
Check(migration.UpOperations.OfType<CreateTableOperation>().Count()==6,"Exactly six catalogue tables");
Check(db.Model.FindEntityType(typeof(Category))!.FindProperty("ParentCategoryId") is null,"Flat categories, no unapproved hierarchy");
Check(db.Model.GetEntityTypes().Where(t => t.ClrType.Namespace==typeof(Product).Namespace).All(t => !t.GetProperties().Any(p => p.IsShadowProperty())),"Explicit relationships without shadow foreign keys");
Check(db.Model.FindEntityType(typeof(ProductVariant))!.FindProperty(nameof(ProductVariant.Price))!.GetPrecision()==18 && db.Model.FindEntityType(typeof(ProductVariant))!.FindProperty(nameof(ProductVariant.Price))!.GetScale()==2,"Price decimal(18,2)");
Check(db.Model.FindEntityType(typeof(ProductUnitConversion))!.FindProperty(nameof(ProductUnitConversion.Factor))!.GetScale()==6,"Conversion decimal(18,6)");
Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"All migrations applied");
var catalogueCountsBefore = new[]{await db.Categories.CountAsync(),await db.Products.CountAsync(),await db.ProductVariants.CountAsync(),await db.UnitsOfMeasure.CountAsync(),await db.ProductUnitConversions.CountAsync(),await db.ProductImages.CountAsync()};
await using(var transaction = await db.Database.BeginTransactionAsync())
{
    var prefix = "smoke-" + Guid.NewGuid().ToString("N")[..8];
    var category = new Category {Name="Temporary test",Slug=prefix};
    var product = new Product {Category=category,Name="Temporary grocery",Slug=prefix+"-product"};
    var other = new Product {Category=category,Name="Other test grocery",Slug=prefix+"-other"};
    UnitOfMeasure Unit(string code,UnitOfMeasureKind kind,int decimals=0) => new(){Code=prefix+code,Name=code,Symbol=code,Kind=kind,DecimalPlaces=decimals};
    var each=Unit("each",UnitOfMeasureKind.Individual);var pack=Unit("pack",UnitOfMeasureKind.Pack);var carton=Unit("carton",UnitOfMeasureKind.Carton);var kg=Unit("kg",UnitOfMeasureKind.Mass,3);var gram=Unit("g",UnitOfMeasureKind.Mass,3);
    ProductVariant Variant(string suffix,Product p,UnitOfMeasure unit) => new(){Product=p,Name=suffix,Sku=prefix+suffix,UnitOfMeasure=unit,Price=12.34m};
    var bottle=Variant("bottle",product,each);bottle.Barcode="00"+Guid.NewGuid().ToString("N");
    var rice5=Variant("rice5",product,pack);var rice25=Variant("rice25",product,pack);
    var measured=Variant("measured",other,kg);measured.AllowFractionalQuantity=true;measured.QuantityStep=.001m;
    db.AddRange(bottle,rice5,rice25,measured,carton,gram);await db.SaveChangesAsync();
    ProductVariant Candidate(string sku,string? barcode=null) => new(){ProductId=product.Id,UnitOfMeasureId=each.Id,Name="Candidate",Sku=sku,Barcode=barcode,Price=1m};
    async Task Reject(object entity,string name,params int[] errorNumbers)
    {
        db.Add(entity);var rejected=false;
        try {await db.SaveChangesAsync();}
        catch(DbUpdateException e) when(e.InnerException is SqlException sql && errorNumbers.Contains(sql.Number)){rejected=true;}
        finally{db.ChangeTracker.Clear();}
        Check(rejected,name);
    }
    Check(await db.ProductVariants.CountAsync(v => v.Id==rice5.Id || v.Id==rice25.Id)==2 && !await db.ProductUnitConversions.AnyAsync(c=>c.ProductVariantId==rice5.Id || c.ProductVariantId==rice25.Id),"25kg/5kg SKUs remain distinct, no inferred interchangeability");
    Check(await db.ProductVariants.CountAsync(v=>v.ProductId==product.Id && v.Barcode==null)==2,"Multiple optional null barcodes allowed");
    await Reject(Candidate(bottle.Sku.ToUpperInvariant()),"Case-insensitive unique SKU",2601,2627);
    await Reject(Candidate(prefix+"duplicate-barcode",bottle.Barcode),"Duplicate barcode rejected",2601,2627);
    await Reject(Candidate(prefix+"empty-barcode",""),"Empty barcode rejected; use null",547);
    await Reject(Candidate(" "),"Blank SKU rejected",547);
    var negative=Candidate(prefix+"negative");negative.Price=-1;await Reject(negative,"Negative price rejected",547);
    // Exercise the original SQL constraint directly; tracked inserts now fail earlier at the mapping boundary.
    var fractionalStepRejected=false;
    try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.ProductVariants SET QuantityStep=0.5 WHERE Id={bottle.Id}"); }
    catch(SqlException e) when(e.Number==547) { fractionalStepRejected=true; }
    Check(fractionalStepRejected,"SQL discrete quantity step rejects fraction");
    var zeroStepRejected=false;
    try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.ProductVariants SET QuantityStep=0 WHERE Id={bottle.Id}"); }
    catch(SqlException e) when(e.Number==547) { zeroStepRejected=true; }
    Check(zeroStepRejected,"SQL zero quantity step rejected");
    ProductUnitConversion Conversion(Guid from,decimal factor) => new(){ProductId=product.Id,ProductVariantId=bottle.Id,FromUnitOfMeasureId=from,ToUnitOfMeasureId=each.Id,Factor=factor};
    db.AddRange(Conversion(pack.Id,6m),Conversion(carton.Id,12m),new ProductUnitConversion {ProductId=other.Id,ProductVariantId=measured.Id,FromUnitOfMeasureId=gram.Id,ToUnitOfMeasureId=kg.Id,Factor=.001m});await db.SaveChangesAsync();
    Check(await db.ProductUnitConversions.CountAsync(c=>c.ProductVariantId==bottle.Id)==2,"Explicit SKU-specific pack/carton factors persisted");
    Check((await db.ProductUnitConversions.AsNoTracking().SingleAsync(c=>c.ProductVariantId==measured.Id)).Factor==.001m && (await db.ProductVariants.AsNoTracking().SingleAsync(v=>v.Id==measured.Id)).QuantityStep==.001m,"Measured conversion and quantity precision round trip");
    Check((await db.ProductVariants.AsNoTracking().SingleAsync(v=>v.Id==bottle.Id)).Price==12.34m,"Price round trip");
    await Reject(Conversion(pack.Id,6),"Duplicate conversion rejected",2601,2627);
    await Reject(new ProductUnitConversion {ProductId=other.Id,ProductVariantId=bottle.Id,FromUnitOfMeasureId=gram.Id,ToUnitOfMeasureId=each.Id,Factor=1},"Conversion cannot reference another product's variant",547);
    await Reject(new ProductUnitConversion {ProductId=product.Id,ProductVariantId=bottle.Id,FromUnitOfMeasureId=gram.Id,ToUnitOfMeasureId=kg.Id,Factor=1},"Conversion must target variant's canonical unit",547);
    await Reject(Conversion(gram.Id,0),"Zero conversion factor rejected",547);
    await Reject(Conversion(gram.Id,-1),"Negative conversion factor rejected",547);
    await Reject(Conversion(each.Id,1),"Self conversion rejected",547);
    await Reject(Unit("bad-pack",UnitOfMeasureKind.Pack,3),"Packaging UOM cannot have fractional precision",547);
    await Reject(Unit("bad-precision",UnitOfMeasureKind.Mass,7),"UOM precision bounded at six",547);
    ProductImage Image(Guid? variant=null,bool primary=false) => new(){ProductId=product.Id,ProductVariantId=variant,Path="/temporary-test.jpg",AltText="Temporary test image",IsPrimary=primary};
    db.AddRange(Image(primary:true),Image(bottle.Id,true),Image(),Image());await db.SaveChangesAsync();
    await Reject(Image(measured.Id),"Variant image must belong to its product",547);
    await Reject(Image(primary:true),"Only one primary product image",2601,2627);
    await Reject(Image(bottle.Id,true),"Only one primary image per variant",2601,2627);
    var badOrder=Image();badOrder.DisplayOrder=-1;await Reject(badOrder,"Image display order nonnegative",547);
    db.ChangeTracker.Clear();
    var beforeVersion=(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==product.Id)).RowVersion;
    await db.Products.Where(p=>p.Id==product.Id).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.IsActive,false));
    Check(!(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==product.Id)).RowVersion.SequenceEqual(beforeVersion),"SQL rowversion updates on catalogue changes");
    var restricted=false;
    try{await db.Categories.Where(c=>c.Id==category.Id).ExecuteDeleteAsync();}
    catch(SqlException e) when(e.Number==547){restricted=true;}
    Check(restricted,"Deleting category cannot cascade through products");
    await transaction.RollbackAsync();
}
db.ChangeTracker.Clear();
var after=new[]{await db.Categories.CountAsync(),await db.Products.CountAsync(),await db.ProductVariants.CountAsync(),await db.UnitsOfMeasure.CountAsync(),await db.ProductUnitConversions.CountAsync(),await db.ProductImages.CountAsync()};
Check(after.SequenceEqual(catalogueCountsBefore),"All temporary catalogue fixtures rolled back");
Console.WriteLine("All SQL Server catalogue foundation tests passed.");
