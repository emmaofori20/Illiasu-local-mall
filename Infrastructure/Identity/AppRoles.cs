namespace ILLiasu_Local_Mall.Infrastructure.Identity;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string InventoryManager = "InventoryManager";
    public const string SalesStaff = "SalesStaff";
    public const string Customer = "Customer";
    public const string AdminPolicy = "AdminArea";
    public const string CategoryPolicy = "CategoryManagement";
    public const string ProductPolicy = "ProductManagement";
    public static readonly string[] All = [Admin, InventoryManager, SalesStaff, Customer];
}
