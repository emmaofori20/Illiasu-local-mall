using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ILLiasu_Local_Mall.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

// IdentityDbContext preserves the standard AspNetUsers/AspNetRoles and related tables.
// Catalogue mappings are additive; no automatic database initialization.
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductUnitConversion> ProductUnitConversions => Set<ProductUnitConversion>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<VariantStockMapping> VariantStockMappings => Set<VariantStockMapping>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!VariantStockMappingValidator.Prepare(this))
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        var existing = Database.CurrentTransaction;
        // Shared ownership is checked under Serializable until commit, not before the write.
        var shared = ChangeTracker.Entries<VariantStockMapping>().Any(e => e.State == EntityState.Added && e.Entity.StockVariantId != e.Entity.ProductVariantId);
        if (shared && existing != null && existing.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable)
            throw new InvalidOperationException("Shared stock mapping writes require a Serializable transaction.");
        await using var owned = existing == null ? await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        await VariantStockMappingValidator.ValidateAsync(this, cancellationToken);
        var count = await base.SaveChangesAsync(owned == null && acceptAllChangesOnSuccess, cancellationToken);
        if (owned != null)
        {
            await owned.CommitAsync(cancellationToken);
            if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
        }
        return count;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
