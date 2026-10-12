using System.Data;
using System.Security.Cryptography;
using System.Text;
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
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name, ContentRootPath = root, EnvironmentName = "Development" });
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
await using var sql = new SqlConnection(connection); await sql.OpenAsync();
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
async Task<object?> Scalar(string text) { await using var c = new SqlCommand(text, sql); return await c.ExecuteScalarAsync(); }
Check((string)(await Scalar("SELECT DB_NAME()"))! == "IlliasuLocalMallDb", "Actual connection targets guarded local database");
var assembly = db.GetService<IMigrationsAssembly>();
var migrationKey = assembly.Migrations.Single(x => x.Key.EndsWith("_AddVariantStockMappings"));
var migration = assembly.CreateMigration(migrationKey.Value, db.Database.ProviderName!);
Check(migration.UpOperations.Count(o => o is CreateTableOperation) == 1 && migration.UpOperations.All(o =>
    o is CreateTableOperation t && t.Name == "VariantStockMappings" || o is CreateIndexOperation i && i.Table == "VariantStockMappings" ||
    o is SqlOperation s && s.Sql.TrimStart().StartsWith("INSERT INTO [dbo].[VariantStockMappings]") && !s.SuppressTransaction), "Migration only creates mappings/indexes and transactionally backfills");
Check(migration.DownOperations.Count == 1 && migration.DownOperations[0] is DropTableOperation d && d.Name == "VariantStockMappings", "Down touches only the new table");
var tables = new[] { "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims", "Categories", "Products", "ProductVariants", "UnitsOfMeasure", "ProductUnitConversions", "ProductImages" };
async Task<Dictionary<string, string>> Snapshot()
{
    var result = new Dictionary<string, string>();
    foreach (var table in tables)
    {
        var keys = (string)(await Scalar($"SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), ',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID('dbo.{table}')"))!;
        await using var command = new SqlCommand($"SELECT * FROM [dbo].[{table}] ORDER BY {keys} FOR JSON PATH, INCLUDE_NULL_VALUES", sql);
        await using var reader = await command.ExecuteReaderAsync(); var json = new StringBuilder();
        while (await reader.ReadAsync()) json.Append(reader.GetString(0));
        result[table] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.ToString())));
        await reader.DisposeAsync();
        result[table + ":object"] = Convert.ToString(await Scalar($"SELECT OBJECT_ID('dbo.{table}')"))!;
    }
    return result;
}
var before = await Snapshot();
if (args.Contains("--apply"))
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    Check(pending.Length == 1 && pending[0] == migrationKey.Key, "Exactly the reviewed migration is pending");
    var backupFolder = Convert.ToString(await Scalar("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))"));
    if (string.IsNullOrWhiteSpace(backupFolder)) throw new InvalidOperationException("SQL Server default backup directory unavailable; configure a server-writable backup path before applying.");
    var backupPath = Path.Combine(backupFolder, "IlliasuLocalMallDb-before-stock-mappings-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".bak");
    await using (var backup = new SqlCommand("BACKUP DATABASE [IlliasuLocalMallDb] TO DISK=@path WITH COPY_ONLY, CHECKSUM", sql) { CommandTimeout = 60 })
    { backup.Parameters.AddWithValue("@path", backupPath); await backup.ExecuteNonQueryAsync(); }
    await using (var verify = new SqlCommand("RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM", sql) { CommandTimeout = 60 })
    { verify.Parameters.AddWithValue("@path", backupPath); await verify.ExecuteNonQueryAsync(); }
    Console.WriteLine("Verified COPY_ONLY backup: " + backupPath);
    var afterBackup = await Snapshot();
    Check(before.All(x => afterBackup[x.Key] == x.Value), "Protected records unchanged during backup");
    await db.GetService<IMigrator>().MigrateAsync(migrationKey.Key);
    Console.WriteLine("Applied " + migrationKey.Key);
    var after = await Snapshot();
    Check(before.All(x => after[x.Key] == x.Value), "All seven Identity and six existing catalogue tables retain object IDs and exact row hashes");
    Check(await db.VariantStockMappings.CountAsync() == await db.ProductVariants.CountAsync() &&
        !await db.VariantStockMappings.AnyAsync(m => m.StockVariantId != m.ProductVariantId || m.UnitsPerSale != 1 || m.SoldUnitOfMeasureId != m.BaseUnitOfMeasureId), "Every legacy variant backfilled independently at factor one");
    Console.WriteLine("Backfilled variant count: " + await db.VariantStockMappings.CountAsync());
}
Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "No pending migrations");
Check(!db.Database.HasPendingModelChanges(), "EF model matches migration snapshot");

// Exercise the migration projection on populated fixtures, even when the real catalogue is empty.
await using (var backfillTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
{
    var tag = "backfill-qa-" + Guid.NewGuid().ToString("N")[..10];
    var unit = new UnitOfMeasure { Code = tag, Name = "Temporary unit", Symbol = "ea", Kind = UnitOfMeasureKind.Individual };
    var product = new Product { Category = new Category { Name = "Temporary backfill", Slug = tag }, Name = "Backfill example", Slug = tag };
    var first = new ProductVariant { Product = product, UnitOfMeasure = unit, Name = "Single", Sku = tag + "-single", Barcode = "00" + tag, Price = 9.87m };
    var second = new ProductVariant { Product = product, UnitOfMeasure = unit, Name = "Independent pack", Sku = tag + "-pack", Price = 51.23m };
    db.AddRange(first, second); await db.SaveChangesAsync();
    await db.VariantStockMappings.Where(m => m.ProductId == product.Id).ExecuteDeleteAsync(); db.ChangeTracker.Clear();
    var backfill = migration.UpOperations.OfType<SqlOperation>().Single().Sql;
    // Limit only the fixture source rows; use the migration's exact six-column projection.
    backfill = backfill.Replace("FROM [dbo].[ProductVariants];", $"FROM [dbo].[ProductVariants] WHERE [ProductId] = '{product.Id}';");
    await db.Database.ExecuteSqlRawAsync(backfill);
    Check(await db.VariantStockMappings.CountAsync(m => m.ProductId == product.Id && m.ProductVariantId == m.StockVariantId && m.UnitsPerSale == 1 && m.SoldUnitOfMeasureId == m.BaseUnitOfMeasureId) == 2,
        "Populated backfill creates two independent factor-one mappings without inference");
    var retained = await db.ProductVariants.AsNoTracking().OrderBy(v => v.Sku).Where(v => v.ProductId == product.Id).ToListAsync();
    Check(retained.Any(v => v.Id == first.Id && v.Price == 9.87m && v.Sku == first.Sku && v.Barcode == first.Barcode) && retained.Any(v => v.Id == second.Id && v.Price == 51.23m), "Backfill preserves populated fixture IDs/prices/SKUs/barcodes");
    await backfillTransaction.RollbackAsync(); db.ChangeTracker.Clear();
}

await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
{
    var prefix = "mapping-qa-" + Guid.NewGuid().ToString("N")[..10];
    var category = new Category { Name = "Temporary mapping test", Slug = prefix };
    var product = new Product { Category = category, Name = "Temporary water", Slug = prefix + "-water" };
    var other = new Product { Category = category, Name = "Other temporary product", Slug = prefix + "-other" };
    var each = new UnitOfMeasure { Code = prefix + "-each", Name = "Each", Symbol = "ea", Kind = UnitOfMeasureKind.Individual };
    var carton = new UnitOfMeasure { Code = prefix + "-ctn", Name = "Carton", Symbol = "ctn", Kind = UnitOfMeasureKind.Carton };
    var kg = new UnitOfMeasure { Code = prefix + "-kg", Name = "Kilogram", Symbol = "kg", Kind = UnitOfMeasureKind.Mass, DecimalPlaces = 6 };
    ProductVariant Variant(string suffix, Product p, UnitOfMeasure u) => new() { Product = p, Name = suffix, Sku = prefix + "-" + suffix, UnitOfMeasure = u, Price = 7.25m };
    var bottle = Variant("bottle", product, each); var sealedCarton = Variant("sealed", product, carton);
    var foreign = Variant("foreign", other, each); var rice = Variant("loose-rice", product, kg); rice.AllowFractionalQuantity = true; rice.QuantityStep = .000001m;
    db.AddRange(bottle, sealedCarton, foreign, rice); await db.SaveChangesAsync();
    Check(await db.VariantStockMappings.CountAsync(m => m.ProductId == product.Id) == 3 && await db.VariantStockMappings.AnyAsync(m => m.ProductVariantId == sealedCarton.Id && m.StockVariantId == sealedCarton.Id && m.UnitsPerSale == 1), "New independent/sealed variants receive exactly one self mapping");
    VariantStockMapping Mapping(ProductVariant sold, ProductVariant owner, decimal factor) => new() { ProductVariantId = sold.Id, ProductId = sold.ProductId, SoldUnitOfMeasureId = sold.UnitOfMeasureId, StockVariantId = owner.Id, BaseUnitOfMeasureId = owner.UnitOfMeasureId, UnitsPerSale = factor };
    var shared = Variant("shared-carton", product, carton); db.Add(shared); db.ChangeTracker.DetectChanges();
    db.Add(Mapping(shared, bottle, 12m)); await db.SaveChangesAsync();
    Check(await db.VariantStockMappings.AnyAsync(m => m.ProductVariantId == shared.Id && m.StockVariantId == bottle.Id && m.UnitsPerSale == 12), "Separately priced carton explicitly shares bottle stock");
    var measured = Variant("measured-option", product, each); db.Add(measured); db.ChangeTracker.DetectChanges(); db.Add(Mapping(measured, rice, .123456m)); await db.SaveChangesAsync();
    Check((await db.VariantStockMappings.AsNoTracking().SingleAsync(m => m.ProductVariantId == measured.Id)).UnitsPerSale == .123456m, "Six-decimal consumption round trip");
    var batchOwner = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = each.Id, Name = "Batch bottle", Sku = prefix + "-batch-owner", Price = 2 };
    var batchCarton = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = carton.Id, Name = "Batch carton", Sku = prefix + "-batch-carton", Price = 19 };
    db.AddRange(batchOwner, batchCarton); db.Add(Mapping(batchCarton, batchOwner, 12)); await db.SaveChangesAsync();
    Check(await db.VariantStockMappings.CountAsync(m => m.ProductVariantId == batchOwner.Id || m.ProductVariantId == batchCarton.Id) == 2, "Pending root and shared option commit together");
    db.ChangeTracker.Clear();
    var failedOwner = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = each.Id, Name = "Rollback owner", Sku = prefix + "-rollback-owner", Price = 2 };
    var failedCarton = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = carton.Id, Name = "Rollback carton", Sku = prefix + "-rollback-carton", Price = -1 };
    db.AddRange(failedOwner, failedCarton); db.Add(Mapping(failedCarton, failedOwner, 12)); var constraintFailed = false;
    try { await db.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is SqlException s && s.Number == 547) { constraintFailed = true; }
    db.ChangeTracker.Clear();
    Check(constraintFailed && !await db.ProductVariants.AnyAsync(v => v.Id == failedOwner.Id || v.Id == failedCarton.Id) &&
        !await db.VariantStockMappings.AnyAsync(m => m.ProductVariantId == failedOwner.Id || m.ProductVariantId == failedCarton.Id), "SQL constraint failure rolls back all pending variants and mappings");
    async Task Reject(Action arrange, string message)
    {
        db.ChangeTracker.Clear(); arrange(); var rejected = false;
        try { await db.SaveChangesAsync(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message); db.ChangeTracker.Clear();
    }
    async Task RejectOption(ProductVariant owner, decimal factor, string message)
    {
        var option = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = carton.Id, Name = message, Sku = prefix + "-" + Guid.NewGuid().ToString("N"), Price = 1 };
        await Reject(() => { db.Add(option); db.Add(Mapping(option, owner, factor)); }, message);
        Check(!await db.ProductVariants.AnyAsync(v => v.Id == option.Id), message + " leaves no partial variant");
    }
    await RejectOption(foreign, 1, "Cross-product ownership rejected");
    await RejectOption(shared, 1, "Ownership chain rejected");
    await RejectOption(bottle, 0, "Zero consumption rejected");
    await RejectOption(bottle, -1, "Negative consumption rejected");
    await RejectOption(rice, .1234567m, "Excess decimal precision rejected without rounding");
    await RejectOption(rice, 1000000000000m, "Decimal overflow rejected");
    await RejectOption(bottle, .5m, "Fractional individual consumption rejected");
    await Reject(() => {
        var missingRoot = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = carton.Id, Name = "Missing root", Sku = prefix + "-missing", Price = 1 };
        db.Add(missingRoot); db.Add(new VariantStockMapping { ProductVariantId = missingRoot.Id, ProductId = product.Id, SoldUnitOfMeasureId = carton.Id, StockVariantId = Guid.NewGuid(), BaseUnitOfMeasureId = each.Id, UnitsPerSale = 12 });
    }, "Missing stock owner rejected");
    await Reject(() => db.Add(Mapping(bottle, bottle, 1)), "Duplicate mapping rejected");
    var cycleA = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = each.Id, Name = "A", Sku = prefix + "-cycleA", Price = 1 };
    var cycleB = new ProductVariant { ProductId = product.Id, UnitOfMeasureId = each.Id, Name = "B", Sku = prefix + "-cycleB", Price = 1 };
    await Reject(() => { db.AddRange(cycleA, cycleB); db.AddRange(Mapping(cycleA, cycleB, 1), Mapping(cycleB, cycleA, 1)); }, "Two-node cycle rejected");
    await Reject(() => { var map = db.VariantStockMappings.Single(m => m.ProductVariantId == bottle.Id); map.UnitsPerSale = 2; }, "Existing mapping changes rejected");
    await Reject(() => db.Remove(db.VariantStockMappings.Single(m => m.ProductVariantId == bottle.Id)), "Existing mapping removal rejected");
    async Task SqlReject(string text, string label, params int[] numbers)
    {
        var rejected = false;
        try { await db.Database.ExecuteSqlRawAsync(text); }
        catch (SqlException e) when (numbers.Contains(e.Number)) { rejected = true; }
        Check(rejected, label);
    }
    await SqlReject($"INSERT INTO dbo.VariantStockMappings VALUES ('{bottle.Id}','{product.Id}','{each.Id}','{bottle.Id}','{each.Id}',1)", "SQL primary key rejects duplicate mapping", 2627, 2601);
    await SqlReject($"UPDATE dbo.VariantStockMappings SET UnitsPerSale=0 WHERE ProductVariantId='{shared.Id}'", "SQL positive-factor constraint enforced", 547);
    await SqlReject($"UPDATE dbo.VariantStockMappings SET UnitsPerSale=2 WHERE ProductVariantId='{bottle.Id}'", "SQL self-owned factor constrained to one", 547);
    await SqlReject($"UPDATE dbo.VariantStockMappings SET StockVariantId='{foreign.Id}' WHERE ProductVariantId='{shared.Id}'", "SQL composite FK rejects cross-product owner", 547);
    await SqlReject($"UPDATE dbo.VariantStockMappings SET BaseUnitOfMeasureId='{carton.Id}' WHERE ProductVariantId='{shared.Id}'", "SQL composite FK rejects wrong base unit", 547);
    await SqlReject($"UPDATE dbo.VariantStockMappings SET UnitsPerSale=1000000000000 WHERE ProductVariantId='{shared.Id}'", "SQL decimal overflow rejected", 8115);
    await SqlReject($"DELETE dbo.ProductVariants WHERE Id='{bottle.Id}'", "No cascading root deletion", 547);
    await transaction.RollbackAsync();
}
db.ChangeTracker.Clear();
await using (var weaker = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
{
    var fake = new VariantStockMapping { ProductVariantId = Guid.NewGuid(), ProductId = Guid.NewGuid(), SoldUnitOfMeasureId = Guid.NewGuid(), StockVariantId = Guid.NewGuid(), BaseUnitOfMeasureId = Guid.NewGuid() };
    db.Add(fake); var rejected = false;
    try { await db.SaveChangesAsync(); } catch (InvalidOperationException e) when (e.Message.Contains("Serializable")) { rejected = true; }
    Check(rejected, "Shared mapping writes reject a weaker caller transaction");
    await weaker.RollbackAsync(); db.ChangeTracker.Clear();
}
var finalSnapshot = await Snapshot();
Check(before.All(x => finalSnapshot[x.Key] == x.Value), "SQL fixtures rolled back; Identity/catalogue records unchanged");
Check(await db.VariantStockMappings.CountAsync() == await db.ProductVariants.CountAsync(), "Exactly one mapping per persisted variant after tests");
Console.WriteLine("Stock mapping SQL integration suite passed.");
