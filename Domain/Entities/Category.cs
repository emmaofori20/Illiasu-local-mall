namespace ILLiasu_Local_Mall.Domain.Entities;

// Flat categories: a hierarchy is not justified by the current approved requirements.
public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
