# Implemented variant stock mapping extension

The subsequent [staff Add Product workflow](product-selling-options.md) now saves explicit
separate/shared mappings. It reuses this schema and validator through shared consumption rules;
no additional migration was required. The extension report below describes its original scope.

Applied 11 October 2026 under the owner's explicit approval of the catalogue database
extension. Migration: **20261011172240_AddVariantStockMappings**. Approval covers this
mapping foundation only; inventory, purchasing, payment and staff form revisions remain
separate tasks. No Identity, existing catalogue schema, design or Tailwind changes.

## Schema and backfill

One new table, `VariantStockMappings`: ProductVariantId primary key, ProductId,
SoldUnitOfMeasureId, StockVariantId, BaseUnitOfMeasureId and UnitsPerSale decimal(18,6).
Two non-cascading composite foreign keys reference the existing
ProductVariants `(Id, ProductId, UnitOfMeasureId)` alternate key. They enforce selling/stock
owner product and UOM identity without new columns on ProductVariants.

Checks enforce positive consumption and, for self-owned mappings, factor one with matching
sold/base units. Indexes cover selling composite identity (unique), StockVariantId lookup
and the stock-owner composite foreign key. Mapping PK permits at most one per variant.

Reviewed C# migration operations and generated SQL: only the new table, its indexes,
transactional INSERT/SELECT backfill and migration-history entry. No ALTER/DROP/recreation
of Identity or existing catalogue tables. Down drops only the new table; reverting after
shared options exist is unsafe without a reviewed data/rollback plan.

Local database contained **zero existing variants** at application time. Backfill inserted
zero rows; it is ready for populated catalogues and was tested against two populated,
rollback-isolated fixtures. Every existing SKU maps to itself with its existing UOM and
factor one. Nothing is inferred from names, units, packaging or SKU patterns. Prices,
barcodes, variants, conversions and image ownership are preserved.

## Validation and write boundary

`VariantStockMappingValidator` is invoked by ApplicationDbContext's synchronous and
asynchronous tracked SaveChanges paths. It provides the shared validation boundary for
application writers; no administration mapping endpoint or new form was added.

- New variants without an explicit mapping receive one self-owned mapping within the
  same save. This preserves the already implemented Product creation service and makes
  mapping creation atomic without changing form/view/controller code.
- Explicit mappings must target a same-product variant with correctly matched units.
  Owners must have a direct self-owned mapping; missing roots, cycles, chains and duplicate
  mappings are rejected against persisted and pending records.
- Positive decimal(18,6) consumption rejects excess precision/overflow rather than rounding.
  Base-unit precision, owner's fractional rules and normalized selling quantity step are
  validated before insertion. Price is never multiplied or inferred.
- Mapping inserts start a Serializable transaction when no caller transaction exists.
  Shared mappings require Serializable when participating in a caller transaction, keeping
  root reads valid through persistence/commit. All pending variants/mappings commit together.
- Existing mappings cannot be modified or deleted through tracked saves. Changes to variant
  quantity rules or unit precision/dimensions are also blocked pending a reviewed revision
  workflow; ordinary price/availability updates are unaffected. There is no retargeting API.

Root ownership and mandatory child-row existence are cross-row application invariants,
not ordinary SQL CHECK constraints. Direct SQL and EF bulk writes bypass SaveChanges.
They are reserved for reviewed migrations and disposable test-fixture setup/cleanup, not
application mapping commands. Future imports/POS adapters must call the validated services;
future stock queries must fail closed on missing/invalid mappings. No trigger was added.
Database permissions and any future direct-writer/trigger policy need review before such
writers are introduced. Decimal column storage cannot reject every over-precision input
before SQL conversion; application validation is the no-rounding boundary.

## Backup and preservation evidence

Before applying, the guarded Development runner confirmed configuration targets Windows-
authenticated Emmanuel / IlliasuLocalMallDb and actual DB_NAME matches. It refuses another
target or additional unexpected pending migrations. COPY_ONLY backup with CHECKSUM was
created in SQL Server's default backup directory and passed RESTORE VERIFYONLY WITH CHECKSUM:

```text
C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IlliasuLocalMallDb-before-stock-mappings-20261011-172542-577d7298.bak
```

No restore was performed. The backup stays outside the source tree and should be protected
as database data. Canonically ordered complete-row SHA-256 hashes and table object IDs
matched before/after for all seven Identity and six existing catalogue tables, including
product prices, SKUs, barcodes and rowversions. Hash inputs and user data were not printed.
No production database was contacted. No automatic startup migration was added.

## Verification and changed files

SQL tests pass for independent/sealed/shared options, pending root/option batch creation,
atomic rollback after a SQL constraint failure, six-decimal round trip, populated
backfill, missing/cross-product roots, chains/cycles, duplicate mappings, zero/negative/
fractional/excess-precision/overflow consumption, immutability and weaker-transaction
rejection. Direct SQL checks verify PK, positive/self-owned checks, composite FKs, decimal
overflow and non-cascading deletion. All mapping fixtures roll back; final record hashes
and exactly-one-mapping counts match.

CatalogueSmoke, ProductCreateSmoke and ProductSmoke regression suites pass, retaining
existing catalogue constraints, authorization, antiforgery, atomic creation and listing
behavior. Product smoke cleanup now removes only fixture mappings before fixture variants.
The existing fractional-step SQL test now exercises its constraint directly because tracked
inserts reject it earlier. .NET builds pass with zero warnings/errors. No packages added.

Changed: Domain/Entities/VariantStockMapping.cs and ProductVariant comments;
Infrastructure/Persistence/ApplicationDbContext.cs, VariantStockMappingValidator.cs,
Configurations/VariantStockMappingConfiguration.cs, migration/designer/snapshot;
tests/StockMappingSmoke project, existing catalogue/product smoke harnesses, and documentation.

```powershell
dotnet build
dotnet build tests/StockMappingSmoke/StockMappingSmoke.csproj
dotnet run --no-build --project tests/StockMappingSmoke/StockMappingSmoke.csproj
```

`--apply` is a one-time guarded backup/application mode; it requires exactly this migration
to be pending and will refuse to reapply it. Normal test mode performs no migration.

No revised Add Product fields, stock balance, costing transaction, purchasing, reservation,
checkout, payment integration or storefront/admin visual change was implemented.
