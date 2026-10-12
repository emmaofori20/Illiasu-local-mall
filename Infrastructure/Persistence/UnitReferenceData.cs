using System.Data;
using ILLiasu_Local_Mall.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

/// <summary>Add-only reference data: never changes existing definitions or packaging conversions.</summary>
public static class UnitReferenceData
{
    public static async Task<int> SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Serialize this command across processes; the unique code index remains authoritative.
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = 'ILLiasu.UnitReferenceData', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @result < 0 THROW 50001, 'Could not acquire unit reference-data lock.', 1;", ct);
        var existing = (await db.UnitsOfMeasure.AsNoTracking().Select(x => x.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var definitions = new (string Code, string Name, UnitOfMeasureKind Kind, int Precision)[]
        {
            ("PCS", "Piece", UnitOfMeasureKind.Individual, 0),
            ("PACK", "Pack", UnitOfMeasureKind.Pack, 0),
            ("CTN", "Carton", UnitOfMeasureKind.Carton, 0),
            ("TRAY", "Tray", UnitOfMeasureKind.Pack, 0),
            ("BAG", "Bag", UnitOfMeasureKind.Pack, 0),
            ("BTL", "Bottle", UnitOfMeasureKind.Individual, 0),
            ("KG", "Kilogram", UnitOfMeasureKind.Mass, 3),
            ("G", "Gram", UnitOfMeasureKind.Mass, 3),
            ("L", "Litre", UnitOfMeasureKind.Volume, 3),
            ("ML", "Millilitre", UnitOfMeasureKind.Volume, 3)
        };
        var added = 0;
        foreach (var unit in definitions.Where(x => !existing.Contains(x.Code)))
        {
            db.UnitsOfMeasure.Add(new UnitOfMeasure { Code = unit.Code, Name = unit.Name,
                Symbol = unit.Code, Kind = unit.Kind, DecimalPlaces = unit.Precision });
            added++;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return added;
    }
}
