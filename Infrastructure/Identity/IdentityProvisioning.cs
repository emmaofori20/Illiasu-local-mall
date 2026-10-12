using ILLiasu_Local_Mall.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Infrastructure.Identity;

public static class IdentityProvisioning
{
    public static void RequireLocalDevelopment(IHostEnvironment environment, string? connection)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Identity setup commands require Development.");
        var target = new SqlConnectionStringBuilder(connection);
        if (!target.DataSource.Equals("Emmanuel", StringComparison.OrdinalIgnoreCase) ||
            !target.InitialCatalog.Equals("IlliasuLocalMallDb", StringComparison.OrdinalIgnoreCase) ||
            !target.IntegratedSecurity)
            throw new InvalidOperationException("Identity setup requires the approved local Windows-authenticated database.");
    }

    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roles)
    {
        foreach (var name in AppRoles.All)
        {
            if (await roles.RoleExistsAsync(name)) continue;
            var result = await roles.CreateAsync(new IdentityRole(name));
            if (!result.Succeeded && !await roles.RoleExistsAsync(name))
                throw new InvalidOperationException("Role seed failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }

    public static async Task RunAsync(IServiceProvider services, IHostEnvironment environment,
        IConfiguration configuration, bool provisionAdmin)
    {
        RequireLocalDevelopment(environment, configuration.GetConnectionString("DefaultConnection"));
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedRolesAsync(roles);
        Console.WriteLine("Roles ready: " + string.Join(", ", AppRoles.All));
        if (!provisionAdmin) return;

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set BootstrapAdmin:Email and BootstrapAdmin:Password privately in Development secrets or environment variables.");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = 'ILLiasu.Identity.AdminBootstrap', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 50000, 'Admin bootstrap lock unavailable', 1;");
        var existingAdmins = await users.GetUsersInRoleAsync(AppRoles.Admin);
        if (existingAdmins.Count > 0)
        {
            if (existingAdmins.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Admin already provisioned; credentials unchanged.");
                await transaction.CommitAsync();
                return;
            }
            throw new InvalidOperationException("An Admin already exists; first-admin provisioning is closed.");
        }
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Existing accounts cannot be elevated by the bootstrap command.");
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException("Admin creation failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        result = await users.AddToRoleAsync(user, AppRoles.Admin);
        if (!result.Succeeded) throw new InvalidOperationException("Admin role assignment failed.");
        await transaction.CommitAsync();
        Console.WriteLine("First Admin provisioned. Remove the bootstrap secrets now.");
    }
}
