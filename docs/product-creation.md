# Product creation

Subsequent [staff selling-option update](product-selling-options.md) adds stable temporary
row keys, separate/shared stock behaviour and atomic VariantStockMapping persistence.
The original creation slice below is retained for historical context; no stock operations exist.

Implemented using the existing catalogue schema. `/Admin/Products/Create` is a dedicated,
responsive two-column MVC form for Admin and InventoryManager only. Other administration
permissions, the Development environment boundary, Identity and the storefront remain unchanged.

## Fields and validation

- Product: name, unique slug (automatic suggestion with explicit manual edit), active category,
  optional description and activation.
- One to 25 independent variants: name, globally unique SKU, optional globally unique barcode,
  active canonical unit, GH₵ price, quantity step, fractional-quantity permission, activation
  and manual availability. Inactive SKUs/barcodes also retain uniqueness.
- Up to ten explicit alternate-unit conversions per variant. The server derives product,
  variant and canonical target ownership; submitted ownership identifiers are ignored.
  Sources must be active, distinct and different from the canonical unit. Factors are positive,
  respect canonical-unit precision and do not imply cross-SKU pooling or conversion prices.

Prices use two decimal places; quantities/factors use up to six and respect the unit definition.
Fractional quantities require mass/volume units. Independently priced packs/cartons need distinct
SKUs. No automatic repacking, stock balances, deductions or inventory transactions are created.

The application service rechecks references and uniqueness inside a SQL Server Serializable
transaction. Product, variants and conversions commit together. Constraint conflicts and
deadlocks return safe validation feedback; errors preserve entered values. Strongly typed
allowlisted inputs, role authorization, antiforgery and bounded requests protect submissions.
Success redirects to the listing. Dynamic rows keep binding indices and label targets aligned.

## Image boundary — intentionally not enabled

Selection, local previews and primary-image selection are implemented. The server boundary
checks declared type, per-file size (5 MB), count (five) and primary index, then rejects every
upload. These preliminary checks are not decoded-image verification. No file is stored and no
ProductImage row is inserted; remove selected files to save a product without images.

Required decisions: durable hosting/storage provider, access/URL policy, decoded-image validator
and safe re-encoding, pixel limits, generated storage identifiers, and cleanup/compensation for
database or storage failures. The boundary must stay closed until that lifecycle is implemented.
Original filenames never become paths. Browser file selections must be reselected after server
validation. No schema changes are proposed by this slice.

Active category and unit definitions must already exist; this task does not add unit management
or silently seed business reference data. Product editing remains a separate task.

## Changed files and verification

- `Application/Catalog/ProductCreateInput.cs`, `ProductCreation.cs`: inputs and atomic service.
- `Areas/Admin/Controllers/ProductsController.cs`, `ViewModels/ProductCreateForm.cs` and
  `Views/Products/Create.cshtml`, `_CreateVariant.cshtml`, `_CreateConversion.cshtml`, `Index.cshtml`.
- `Infrastructure/Storage/ProductImageUploadBoundary.cs`, `Program.cs` registrations.
- `wwwroot/js/product-create.js`, `Styles/components.css` and generated `wwwroot/css/app.css`.
- `tests/ProductCreateSmoke/*`, updated ProductSmoke expectations and catalogue documentation.

SQL/HTTP smoke tests cover single/multiple variants, duplicate SKU/barcode, negative and
over-precision prices, conversion validation/ownership, missing references, role checks,
antiforgery and rollback after database insertion before commit. The closed upload boundary
rejects spoofed files. Browser checks exercise multi-variant submission, conversion renumbering,
duplicate-SKU field errors, preserved values and summary focus. Screenshots inspected at
375, 768 and 1440px show Inter, responsive stacking and no page overflow. Disposable QA
records/accounts are removed after each run; no migration or package was added.

```powershell
npm.cmd run css:prod
dotnet build
dotnet build tests/ProductCreateSmoke/ProductCreateSmoke.csproj
# Run MVC in Development at https://localhost:7116, then:
dotnet run --no-build --project tests/ProductCreateSmoke/ProductCreateSmoke.csproj
# Optional fixture retention for browser checks:
dotnet run --no-build --project tests/ProductCreateSmoke/ProductCreateSmoke.csproj -- --preview
node tests/ProductCreateSmoke/preview.cjs
```

Browser checks require headless Edge CDP on port 9225. Temporary fixture cookies remain in OS
temporary storage only; the preview runner signals cleanup and fixtures otherwise expire after
ten minutes. Build harnesses before starting MVC to avoid Windows apphost file locks.
