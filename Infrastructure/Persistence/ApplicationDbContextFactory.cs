using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

// Design-time commands are deliberately restricted to the approved local database.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory()
        });
        var connection = builder.Configuration.GetConnectionString("DefaultConnection");
        IdentityProvisioning.RequireLocalDevelopment(builder.Environment, connection);
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection).Options);
    }
}
