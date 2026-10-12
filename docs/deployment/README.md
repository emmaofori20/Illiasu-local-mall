# Production SQL migration artifact

For the subsequently implemented explicit provisioning command, target guards,
safe combined SQLCMD fallback and recovery procedure, see
[database-provisioning.md](../database-provisioning.md). The original schema artifact
and three migrations remain unchanged.

Generated locally on 12 October 2026 using the existing SQL Server migrations and
EF Core CLI/provider/Identity packages 8.0.31. The project targets .NET 8.

Script: [illiasu-production-idempotent.sql](illiasu-production-idempotent.sql)

This schema artifact was generated, not executed. No database was connected to or modified during its generation.
No migration, context, configuration, safety guard or application startup code was
changed. No admin credentials, users, roles or reference selling units were seeded.

## Included migration chain

| Order | Migration | Contents |
| --- | --- | --- |
| 1 | `20261011000039_InitialIdentity` | Seven standard Identity tables and their keys/indexes/relationships |
| 2 | `20261011002028_AddCatalogueFoundation` | Categories, UnitsOfMeasure, Products, ProductVariants, ProductImages, ProductUnitConversions, constraints and indexes |
| 3 | `20261011172240_AddVariantStockMappings` | VariantStockMappings, relationships/indexes/check constraints, and existing-variant backfill |

The script creates `__EFMigrationsHistory` if absent and records each migration with
ProductVersion 8.0.31. Each migration's operations are guarded by its history entry
and enclosed in its own transaction. There are 15 table definitions in total.
There are no database creation, database selection, schema-drop or Identity data
insertion statements in this forward script.

The stock-mapping backfill is part of the existing migration, not added seeding:
it maps each existing ProductVariant to itself with UnitsPerSale=1. On an empty
database this inserts no rows. It has been preserved unchanged.

## Verification

- Release build passed with zero warnings/errors.
- `dotnet ef migrations list --no-connect` discovered exactly the three migration
  IDs above in order. Each has an Up/Down implementation and designer metadata
  associated with ApplicationDbContext; the latest model snapshot is present.
- `dotnet ef migrations has-pending-model-changes` reported:
  `No changes have been made to the model since the last migration.`
- Generated the complete forward chain from `0` (before any migration) to the
  explicit latest ID using `--idempotent`.
- Inspected all 15 table definitions, all three migration-history inserts, three
  transaction/commit pairs, and the existing guarded backfill. No credentials or
  connection string are included in the SQL.
- Database execution, repeated execution and hosted-schema compatibility were not
  tested, because this task is limited to offline preparation. The migration-list
  command cannot report applied/pending database status with `--no-connect`; this
  is expected and does not indicate an incomplete source migration chain.

SHA-256 of generated SQL bytes:
`0D4B477C53F535CEA438D8BB93A15002526F14EDC2A4D9A0954BBA439FD807F7`
Git line-ending conversion may change this byte checksum without changing SQL.

## Assumptions and application blockers

- The target SQL Server database already exists. The script does not create the
  database or select one with USE. Confirm the intended database separately before
  any later authorized execution.
- Use the `dbo` default schema. EF's table/history DDL is unqualified, while the
  existing stock-mapping backfill explicitly references `[dbo]` tables. A different
  executing principal's default schema can cause failure or misplaced objects.
- Existing databases must have a truthful `__EFMigrationsHistory` and a schema
  consistent with the recorded migrations. Idempotence skips recorded migrations;
  it does not repair schema drift or safely adopt pre-existing untracked tables.
  A partially applied/untracked schema requires investigation first.
- A future authorized deployment operator needs DDL/index/constraint permissions,
  history-table writes and stock-backfill writes, plus a SQL client that processes
  `GO` batch separators on a single connection. Stop on errors rather than continuing
  subsequent batches. Back up existing data and review the artifact before applying.
- Application roles (especially Customer, required by registration) and selling
  units remain unseeded. Existing setup commands deliberately accept only the
  approved local Development database. Production provisioning needs a separately
  approved procedure; do not relax those guards or expose admin credentials.
- This reproduces only the existing migration chain. It adds no suppliers,
  purchasing transactions or inventory ledger. Generating the existing stock-mapping
  SQL does not grant commercial approval to implement gated inventory additions.

There is no blocker to offline generation. Target history/schema/default schema,
permissions, scope approval where applicable and separate role/reference-data
provisioning are not verified and remain prerequisites for production application.

## Repeat generation without database access

Restore the pinned tool with `dotnet tool restore`, then build the project in
Release. Run the following under the existing guarded Development design-time
configuration, scoped to the current process and restored afterward. Use only the
approved password-free local connection descriptor; do not supply a hosted target.
The factory validates the descriptor without opening the connection.

```powershell
$priorAsp = $env:ASPNETCORE_ENVIRONMENT
$priorDot = $env:DOTNET_ENVIRONMENT
$priorConnection = $env:ConnectionStrings__DefaultConnection
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = 'Server=Emmanuel;Database=IlliasuLocalMallDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
    dotnet build 'ILLiasu Local Mall.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet ef migrations list --no-connect --no-build --configuration Release --context ApplicationDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Migration discovery failed' }
    dotnet ef migrations has-pending-model-changes --no-build --configuration Release --context ApplicationDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Model differs from snapshot' }
    dotnet ef migrations script 0 20261011172240_AddVariantStockMappings --idempotent --no-build --configuration Release --context ApplicationDbContext --output docs/deployment/illiasu-production-idempotent.sql
    if ($LASTEXITCODE -ne 0) { throw 'SQL generation failed' }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $priorAsp
    $env:DOTNET_ENVIRONMENT = $priorDot
    $env:ConnectionStrings__DefaultConnection = $priorConnection
}
```

The local certificate bypass in this descriptor is not a production TLS setting
and is not embedded in the SQL. No `database update`, application setup commands,
`Migrate` or `EnsureCreated` calls are involved. This document and SQL live under
docs, outside public static files, and are not part of the MVC publish output.

[Microsoft's idempotent migration-script documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
explains the history-based checks used by the generated script.
