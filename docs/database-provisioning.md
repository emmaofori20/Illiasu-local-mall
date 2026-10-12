# Explicit database provisioning and deployment

Implemented 12 October 2026. Normal web startup does not connect for initialization,
create a database, apply migrations or seed data. Provisioning is a separate console
invocation of the same application; it exits before starting the web server.
No HTTP setup endpoint, scheduled migration, default account or password was added.

The three existing migrations and design-time factory are unchanged:
`20261011000039_InitialIdentity`, `20261011002028_AddCatalogueFoundation`,
`20261011172240_AddVariantStockMappings`. EF's `__EFMigrationsHistory` remains the
authority for pending migrations. Never delete or fabricate its rows to force retries.

## Command behavior

Run from the project directory, or from the published folder using
`dotnet "ILLiasu Local Mall.dll"` with the same arguments. Use the DLL built from
the deployment commit. The CLI does not require `dotnet-ef` at execution time.

```powershell
dotnet run --no-launch-profile -c Release -- --database-provision --help
```

`--check` is the default; it opens the target and reads metadata without schema/data
changes. `--apply` requires exact server endpoint and database confirmations. It
acquires a session application lock, validates history/model, and applies only
pending migrations. Unknown history, gaps, a changed model, untracked existing
tables without history, and a non-dbo default schema cause refusal. The command
does not fully detect drift in objects belonging to already recorded migrations;
investigate manually modified schemas before applying.

`--seed-reference-data` requires `--apply` and calls the existing Identity role
service and reference-unit service. It creates only Admin, InventoryManager,
SalesStaff and Customer roles, plus the ten approved units. Existing definitions,
inactive units and identifiers are preserved. It does not create users or grant
role membership. `--seed-only --seed-reference-data --apply` refuses pending
migrations and performs no schema changes, for use after a reviewed SQL deployment.

Legacy `--seed-identity`, `--seed-catalogue-units`, `--provision-admin` and the EF
factory keep their original exact-local/Development guards. They cannot be mixed
with the new command. Production authorization is specific to the new workflow;
it does not relax existing setup/admin restrictions.

### Missing database versus failed connection

The CLI first opens the exact target. Errors such as credentials, network, timeout
and TLS failures do not justify creation. Error 4060 can mean inaccessible as well
as absent: it only permits an additional metadata inspection, not creation. A
successful master connection with sysadmin, ALTER ANY DATABASE or CREATE DATABASE
permission in master (enough to expose even offline databases)
must show no row for the target to prove absence. VIEW ANY DATABASE alone is not
sufficient to prove absence because offline databases can be hidden. Hidden metadata, unavailable
master, offline databases and inaccessible existing databases refuse creation.
Mixed login failures refuse the missing-database path altogether.

Local creation needs all of: Development, the unchanged approved target guard,
matching confirmations, `--apply`, `--create-local-database` and proven absence.
Creation is serialized in master and metadata is rechecked under the lock. No
drop/recreate path exists. Production never creates a database: use the host's
Control Panel. An EF interceptor also rejects implicit CREATE DATABASE attempts
by the migrator, including if the target disappears after the initial check.

## Authorized local development

Use the existing Development user secret for Windows-authenticated
`Emmanuel / IlliasuLocalMallDb`. Do not point Development at the hosted database.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile -c Release -- --database-provision --check
dotnet run --no-launch-profile -c Release -- --database-provision --apply --confirm-server Emmanuel --confirm-database IlliasuLocalMallDb --seed-reference-data
```

For an actually absent approved local database, add `--create-local-database` to
the apply invocation. An account lacking metadata/create permissions must ask the
local DBA to create it; do not broaden the guard or reinterpret login failures.
Save/restore environment overrides in shared shells after use. The command uses
the application's privately configured DefaultConnection; no connection string
argument is accepted and no credential is logged.

## MonsterASP.NET Free-plan procedure

The website deployment flow is not a console session. Do not assume a Free-plan
SSH/terminal or arbitrary process runner, put migrations into Git Build/publish,
or add a setup web endpoint. The host documents planned HTTP tasks as Premium-only;
they are not a migration solution. Execute the published console command on your
own trusted workstation with .NET 8, connecting to SQL remotely only when permitted,
or use the reviewed SQL fallback below. Nothing in this implementation enables
remote access or runs against MonsterASP.NET.

1. Create the hosted SQL database in the Control Panel if needed. Confirm its real
   name (not only its friendly label), server, dbo schema and deployment permissions.
   Back up existing data and pause application writes for an approved maintenance
   window. Record the deployment commit and script/artifact checksum.
2. For workstation CLI execution, enable remote database access and any available
   IP restriction. Use the public endpoint provided by the host, not its internal
   website connection address. Confirm that your account/plan permits access. Do
   not brute-force credentials or repeatedly retry failed logins.
3. Resolve TLS trust **before running**. This project requires encryption with
   certificate validation for Production. MonsterASP.NET's published instructions
   specify non-publicly trusted SQL certificates and remote strings using
   `TrustServerCertificate=True`. Such strings are intentionally rejected by this
   CLI. Obtain an approved certificate-trust solution with the host/security owner,
   or use the host-side reviewed SQL fallback. Do not set Development, disable
   encryption, or weaken this guard to make the CLI connect. No current hosted TLS
   settings have been read or changed.
4. Publish locally (`npm ci`, `npm run css:prod`, Release publish) and open a terminal
   in the resulting folder. Privately populate a process-scoped connection setting
   using a masked prompt, without putting it into scripts/history/config files:

```powershell
$privateConnection = Read-Host 'Approved encrypted SQL connection string' -AsSecureString
$previousConnection = $env:ConnectionStrings__DefaultConnection
$previousAsp = $env:ASPNETCORE_ENVIRONMENT
$previousDot = $env:DOTNET_ENVIRONMENT
try {
    $env:ConnectionStrings__DefaultConnection = [System.Net.NetworkCredential]::new('', $privateConnection).Password
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:DOTNET_ENVIRONMENT = 'Production'
    # Replace placeholders with the exact public endpoint and actual database.
    dotnet 'ILLiasu Local Mall.dll' --database-provision --check --allow-production --confirm-server '<public-endpoint>' --confirm-database '<actual-database>'
    if ($LASTEXITCODE -ne 0) { throw 'Check failed; do not apply.' }
    # Review the reported pending migration IDs and confirm maintenance approval.
    # Run the following separately only after that review, within this private session:
    # dotnet 'ILLiasu Local Mall.dll' --database-provision --apply --allow-production --confirm-server '<public-endpoint>' --confirm-database '<actual-database>' --seed-reference-data
} finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousAsp
    $env:DOTNET_ENVIRONMENT = $previousDot
    $privateConnection.Dispose()
}
```

The apply command is intentionally commented in the sample: review the check,
then explicitly invoke it. Exact confirmation values are compared to the parsed
connection's endpoint/database before any network access. Server aliases must be
confirmed as configured. Successful connection also verifies DB_NAME and dbo.

5. Check the exit code and repeat `--check`; require no pending migrations. Verify
   roles/reference data and account registration over HTTPS. Restore private
   overrides, restrict remote access again and revoke temporary elevated access.
6. In MonsterASP.NET Git Deploy select **Build .NET app** and the MVC project, pull
   the approved commit and publish. Keep the existing production connection private
   in the host's configuration, with both environment names Production. Provisioning
   is not part of this publish and admin pages remain Development-only.

### SQL fallback without a host console or remote CLI

- Schema: [illiasu-production-idempotent.sql](deployment/illiasu-production-idempotent.sql)
- Reference data: [illiasu-reference-data-idempotent.sql](deployment/illiasu-reference-data-idempotent.sql)
- Guarded combined SQLCMD artifact: [illiasu-production-provisioning.sql](deployment/illiasu-production-provisioning.sql)

Schema generation uses the existing guarded factory's Development/local descriptor
offline; see [generation procedure](deployment/README.md). Do not change the factory
or run `dotnet ef database update` against Production. Generate reference SQL with:

```powershell
dotnet run --no-launch-profile -c Release -- --database-provision --export-reference-sql docs/deployment/illiasu-reference-data-idempotent.sql
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-database-fallback.ps1
```

The export reads the approved roles/units and migration metadata without connecting
to a database. The wrapper sets XACT_ABORT and SQLCMD `:ON ERROR EXIT`, checks
explicit confirmed server/database and dbo before any schema operation, and appends
the existing idempotent migration chain followed by add-only reference SQL. Required
SQL Server SET options for filtered indexes are explicit, including QUOTED_IDENTIFIER.
Reference
data runs under Serializable, the provisioning lock and TRY/CATCH rollback. No users
or credentials are included. It checks all three schema migration prerequisites.

Before execution, use a trusted database console to read
`SELECT SERVERPROPERTY('ServerName'), DB_NAME(), SCHEMA_NAME();`, independently
confirm the intended target, and replace only `__CONFIRM_SERVER__` and
`__CONFIRM_DATABASE__` in the combined artifact's two `:setvar` lines with those
verified names. The SQL server identity may differ from a connection endpoint alias.
Never substitute a password; SQLCMD names must be quoted/escaped correctly.

Execute through a SQLCMD-capable client that stops on errors (for example SSMS in
SQLCMD mode or sqlcmd with `-b`) after approval. If using MonsterASP.NET's host-side
Import tool, submit the **combined guarded artifact**, which runs under sqlcmd,
and verify that the tool honors `:ON ERROR EXIT` and the target guards. If directives
are unavailable, do not remove them to make import work; use a supported interactive
execution path or host support instead. Raw Import normally continues after errors,
so unattended import of the original raw migration file is not recommended.
Inspect every import log before declaring success. Never upload these scripts to
the public website folder. The tools remain an operator action, not automatic setup.

Idempotence assumes truthful migration history and schema consistency. It does not
repair drift or untracked/partially created objects. No DROP, database creation or
history reset is included. Keep previous migration IDs/operations immutable.

## Logging and recovery

The console logs target names, mode, connectivity category, applied/pending IDs,
completed phases and added-unit counts. SQL errors report only numbers/states and
phase; provider exception text and secrets are suppressed. Record the output in
a private operator log. Normal application logging/configuration is unchanged.

| Exit | Meaning |
| --- | --- |
| 0 | Check/apply/export completed |
| 2 | Safety/options/history/model refusal |
| 3 | Proven missing database, not created |
| 4 | Connectivity/availability failure |
| 5 | Migration/seed/other operational failure |
| 130 | Cancelled |

Migration application is not one transaction spanning the entire chain. Earlier
migrations may have committed before a later failure. Roles are add-only operations;
unit seeding has its own Serializable transaction. A seed failure does not roll back
completed migrations or role additions. No destructive automatic rollback/retry exists.

On failure:

1. Stop further deployment/application writes. Retain logs, artifact and backup.
2. For login/TLS/network errors fix those causes; never create a database because
   access failed. Ambiguous absence requires the host/DBA to confirm the database.
3. Inspect `__EFMigrationsHistory` and actual objects using an authorized console.
   A normally rolled-back migration can be retried after fixing the cause; a schema
   mismatch or partial untracked operation needs DBA reconciliation or a reviewed
   backup restore. Do not remove history rows or drop the database as a repair.
4. Run `--check` again and review pending IDs before a separately authorized apply.
   If only seeding failed and no migrations are pending, repeat seed-only or the
   reference SQL. Existing definitions remain preserved; no admin is bootstrapped.
5. Re-verify account flow and disable temporary elevated access before reopening.

## Local verification and limits

```powershell
dotnet build 'ILLiasu Local Mall.csproj' -c Release
dotnet run --project tests/DatabaseProvisioningSmoke -c Release
# Optional: approved local database only, with existing private Development config:
dotnet run --project tests/DatabaseProvisioningSmoke -c Release -- --local-integration
```

Offline tests covered 42 safety/export checks, including production refusal before
DB service resolution, credential-log suppression, target/TLS guards, ambiguous
absence and invalid history. The local integration run used the existing approved
database, with all three migrations already applied: connectivity succeeded,
pending was empty, repeated role/unit seeding added zero units and preserved IDs,
users and migration history. Reference SQL was also rerun locally to verify
preservation. SQLCMD execution also refused the untouched confirmation placeholders
before migrations, then the explicitly confirmed combined fallback succeeded twice
against the fully migrated approved local target. Release build and publish passed;
the scripts/tests/docs are absent from publish. Fresh-database creation and applying the chain on an empty database
were not exercised by these tests; no disposable extra database was authorized by
the unchanged guard. No hosted connection, Production migration or hosted import
was performed. Host permissions/TLS/script execution remain deployment prerequisites.

## Host documentation

- [Remote access and public/internal endpoints](https://help.monsterasp.net/databases/mssql/remote-access)
- [Host SQL TLS settings](https://help.monsterasp.net/databases/mssql/secure-connection)
- [SQL Import behavior](https://help.monsterasp.net/databases/mssql/import-sql-file)
- [Premium-only HTTP planned tasks](https://help.monsterasp.net/websites/configuration/scheduled-tasks)
