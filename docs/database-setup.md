# EF Core and SQL Server foundation

The .NET 8 MVC project uses Microsoft.EntityFrameworkCore.SqlServer,
Microsoft.EntityFrameworkCore.Design and Microsoft.AspNetCore.Identity.EntityFrameworkCore
**8.0.31**. The design package is private build tooling. No migrations or database
creation/update commands run at startup.

`Infrastructure/Identity/ApplicationUser.cs` inherits `IdentityUser` without custom
profile fields. `Infrastructure/Persistence/ApplicationDbContext.cs` inherits
`IdentityDbContext<ApplicationUser>` and preserves the standard Identity mappings:
AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins,
AspNetUserTokens and AspNetRoleClaims. The subsequent Identity slice has now created these seven tables locally and enabled
Identity services, cookies, role seeding and account UI; see [identity-setup.md](identity-setup.md).

## Local connection

Run from the project directory in PowerShell. User secrets are already initialized;
their identifier in the project file is not a credential. A password-free development
connection was saved outside the repository, using Emmanuel / IlliasuLocalMallDb and Windows authentication.
Replace the server with your installed instance as needed:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=Emmanuel;Database=IlliasuLocalMallDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"

# Alternative: SQL Server Express instance
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=ILLiasuLocalMall;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"

# Alternative: LocalDB, only if installed
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=ILLiasuLocalMall;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
```

Choose one connection. The database name is a proposed development name; these commands
do not create it. SQL Server must be installed/running and your Windows account must
have the appropriate access before future database operations can succeed.

Alternatively, set a process-scoped environment variable (takes precedence over secrets):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=Emmanuel;Database=IlliasuLocalMallDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

# Remove the override from this shell when finished
Remove-Item Env:ConnectionStrings__DefaultConnection
```

User secrets load in Development. `Program.cs` registers a scoped SQL Server context;
missing configuration produces a clear error when the context is resolved. The current
prototype pages do not query the context and do not require a database connection.
Do not store a SQL-authentication password in appsettings, launchSettings, source files
or committed scripts. If SQL authentication is required, set the connection privately
through user secrets or the deployment environment's secret configuration.

`TrustServerCertificate=True` is for the local development certificate only. Production
must use deployment secrets, encryption and a trusted server certificate; do not copy
this certificate bypass into production. User secrets are development storage, not an
encrypted production secret vault.

## Verify / next stage

```powershell
dotnet restore
dotnet list package --no-restore
dotnet build
npm.cmd run css:prod
```

The later Identity slice verified connectivity and applied InitialIdentity only to the
approved Emmanuel / IlliasuLocalMallDb local target. Do not apply other migrations or
production updates without authorization. Runtime EnsureCreated/Migrate remain absent.
Admin authorization now requires Admin membership and the dashboard retains its
Development-only prototype gate.
