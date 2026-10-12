namespace ILLiasu_Local_Mall.Domain.Entities;

public enum UnitOfMeasureKind { Individual = 0, Pack = 1, Carton = 2, Mass = 3, Volume = 4 }

public sealed class UnitOfMeasure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public UnitOfMeasureKind Kind { get; set; }
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
