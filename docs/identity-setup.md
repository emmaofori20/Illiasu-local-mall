# Identity setup and local verification

Identity uses the existing ApplicationUser and ApplicationDbContext with cookie authentication.
Migration **20261011000039_InitialIdentity** was applied only to the approved local
Windows-authenticated `Emmanuel` / `IlliasuLocalMallDb` database.

The seven standard tables are AspNetUsers, AspNetRoles, AspNetUserRoles,
AspNetUserClaims, AspNetUserLogins, AspNetUserTokens and AspNetRoleClaims.
`__EFMigrationsHistory` is EF bookkeeping, not an eighth Identity table. No commerce entities exist.

## Local operation

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet tool restore
dotnet build
dotnet run --no-build --no-launch-profile -- --seed-identity
dotnet run --launch-profile https
```

Use `https://localhost:7116/Account/Register`, `/Account/Login`, `/Account` and
`/Account/Logout`. The storefront Account action now links to the working account
pages while retaining its existing styling. Logout requires an antiforgery-protected
POST; its GET page only asks for confirmation.

Role seeding is an explicit idempotent setup command, not a database mutation on
every web startup. It creates Admin, InventoryManager, SalesStaff and Customer.
Run it before opening registration. New public accounts receive only Customer,
regardless of submitted role fields. InventoryManager/SalesStaff currently have no
Admin Area access; module-specific staff policies are a later approved slice.

All current/future MVC Admin Area controllers receive the AdminArea policy through
the application convention, and Overview explicitly requires Admin too. The sample
dashboard retains its Development-only gate; Identity does not make the sample
management buttons real workflows. No production database was migrated.

## First administrator

No permanent administrator is created automatically. Provision one explicitly from
the project directory with a private, masked password prompt:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:BootstrapAdmin__Email = Read-Host "First administrator email"
$adminPassword = Read-Host "Password (12+ characters, upper/lowercase, number and symbol)" -AsSecureString
try {
    $env:BootstrapAdmin__Password = [System.Net.NetworkCredential]::new("", $adminPassword).Password
    dotnet run --no-build --no-launch-profile -- --provision-admin
} finally {
    Remove-Item Env:BootstrapAdmin__Email -ErrorAction SilentlyContinue
    Remove-Item Env:BootstrapAdmin__Password -ErrorAction SilentlyContinue
    $adminPassword.Dispose()
}
```

Alternatively use private `BootstrapAdmin:Email` / `BootstrapAdmin:Password` user
secrets and remove them immediately afterward. Never put them in source or appsettings.
The command requires Development, the exact approved database and Windows Authentication.
A transactional SQL application lock serializes bootstrap attempts. It refuses to
elevate an existing public account or provision another first admin after one exists.
Repeating it for the already provisioned admin leaves credentials unchanged.
There is no bootstrap web endpoint and no hardcoded password.

## Security and remaining scope

Application cookies use the `__Host-ILLiasu.Identity` name, Secure, HttpOnly,
SameSite=Lax and a 30-minute sliding lifetime. Login sessions are not persistent
browser cookies. HTTPS is required; HTTP-only development does not support sign-in.
Security stamps revalidate every five minutes. Passwords require 12 characters,
uppercase, lowercase, a digit and a symbol; five failed logins lock the account
for 15 minutes. Login/registration POST endpoints are rate-limited by remote IP.
Antiforgery applies to browser mutations and return URLs are restricted to local paths.

Email delivery/confirmation, password recovery, MFA and module staff permissions
are not implemented in this slice. Accounts can sign in without confirmed email;
no verified-email claim is implied. Deployment requires appropriately protected
Data Protection keys and approved production configuration; no production migrations
or automatic runtime `Migrate`/`EnsureCreated` calls were added.

## Repeatable tests

With the Development app running on local HTTPS:

```powershell
dotnet run --project tests/IdentitySmoke/IdentitySmoke.csproj -p:BuildProjectReferences=false
```

These SQL-backed tests are restricted to the approved local database. They create
randomly named/generated-credential fixtures and remove them in `finally`. They
verify standard tables, repeated role seeding, registration/role injection, password
policy, secure cookie flags, local redirects, login/lockout/logout, antiforgery,
customer/staff denial and Admin access, plus bootstrap guards and idempotency.
The test HTTP client bypasses the certificate check only for the fixed loopback
HTTPS endpoint; application cookie/certificate security is not weakened.

Rebuild/restart the app before repeated suites to reset the in-memory endpoint rate
limiter. No permanent admin credentials or test customer data are retained.

## Verified result (11 October 2026)

Tailwind production compilation and dotnet build passed with zero .NET warnings/errors. The SQL-backed smoke suite passed, including bootstrap idempotency/guards. The seven Identity tables and migration history are present, exactly four roles remain, and generated test users were removed. Login/Register were inspected at 375/768/1440px with Inter and no overflow. No permanent administrator was retained; provision your own using the masked prompt above. An initial design-time configuration issue was corrected before migration application; no outstanding migration or authentication test errors remain.
