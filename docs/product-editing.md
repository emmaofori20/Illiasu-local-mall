# Product editing

The dedicated Admin Edit Product page replaces the holding page. It reuses Add Product's two-column layout, shared selling-option/conversion partials, Inter, Tailwind v4 and semantic ILLiasu tokens. Existing options appear in separate cards with immutable stock summaries. New options use the same separate/shared controls as creation. No schema or migration change is required.

## Writable and protected fields

- Product: name, description, category and activation. The existing URL slug is retained.
- Existing selling options: independently entered price, optional unique barcode, activation and manual availability.
- Existing option name, SKU, canonical unit, quantity increment/fractional policy, receiving conversions, stock owner and UnitsPerSale are read-only. They require a reviewed revision workflow. No existing option can be removed; deactivate it instead.
- New options: full creation fields, including explicit separate/shared mappings. A shared option can target an existing independent same-product option or a new independent option. Stable keys are existing variant IDs for persisted roots and temporary UUIDs for new rows. These keys are resolved only against the loaded product and submitted new options; external IDs, cycles and chains fail validation.

An unchanged inactive category or unit can be retained for historical records. Category changes and new option/receiving units require active definitions. The form identifies an inactive current category; listing visibility warnings remain. Availability is still manual and no stock balance is created or changed.

## Validation, concurrency and atomicity

ProductsController retains the Admin/InventoryManager policy, Development restriction and antiforgery checks. Strongly typed existing-option inputs expose only writable fields; submitted SKU/unit/stock mapping fields are ignored. Missing, duplicate or foreign existing-option IDs cannot remove or alter historical references. The complete existing option set must be submitted.

ProductEditing reconstructs authoritative identities and mappings from SQL and reuses ProductCreation's catalogue/stock validation. Global active/inactive SKU and barcode uniqueness, price precision, unit precision and direct-root ownership rules remain authoritative. SQL indexes and the DbContext mapping validator provide the final persistence boundary.

One Serializable transaction validates and commits product changes, existing option updates, new options, mappings and receiving conversions. Product and every existing variant rowversion must match the submitted snapshot. EF UPDATE predicates also use original rowversions. Every successful edit advances the product token, including option-only edits or additions. A stale page must reload and review current values; tokens are not silently refreshed to enable overwriting. Validation failures preserve the entered values and original tokens, with field errors and focus links.

No image editing/upload workflow is introduced. Barcode reassignment between two existing options can require clearing the old barcode first because SQL's unique index is immediate; this workflow does not bypass uniqueness for swaps. Maximum 25 total options, including inactive ones, retains the creation limit.

## Add Product stock-source inspection

Browser tests verify that separately counted options appear after a selling unit is selected, including after dynamic additions and earlier-row removal. Incomplete options without a unit are now omitted from choices in both Razor and JavaScript. Shared options remain ineligible as sources. Invalid previous references remain visible for correction rather than being silently retargeted. No packaging or sharing factor is inferred.

## Verification

`tests/ProductEditSmoke` uses disposable fixtures against guarded Development Emmanuel / IlliasuLocalMallDb. It tests successful edits, mutable-field persistence, preserved identifiers/mappings, global and within-product duplicate barcodes, invalid references, new independent/shared options, duplicate SKUs, stale/malformed product and variant tokens, a concurrency conflict during EF saving, post-save rollback, authorization, antiforgery, overposting, and HTTP validation feedback.

Its browser preview tests 375px, 768px and 1440px for Inter, overflow, read-only identities, eligible existing/new roots, dynamic selection preservation, summaries, label/error associations, real saving and validation retention/focus. ProductCreateSmoke and ProductSmoke cover regression behaviour. Test products/categories/accounts are removed; shared unit reference data is preserved.

```powershell
dotnet build tests/ProductEditSmoke/ProductEditSmoke.csproj
# Run the MVC HTTPS development server separately.
dotnet run --no-build --project tests/ProductEditSmoke
npm run css:prod
dotnet build
```

No Identity tables, migrations, inventory balances/deductions, purchasing, POS or payments were modified or implemented.

## Changed files

- `Application/Catalog/ProductEditInput.cs`, `ProductEditing.cs` and `ProductCreation.cs` (shared validation).
- `Areas/Admin/Controllers/ProductsController.cs`, `ViewModels/ProductEditForm.cs`.
- `Areas/Admin/Views/Products/Edit.cshtml`, `_EditVariant.cshtml`, `_StockBehaviour.cshtml`, `_Actions.cshtml`; obsolete `ComingSoon.cshtml` removed.
- `wwwroot/js/product-create.js`, generated `wwwroot/css/app.css`, and `Program.cs` service registration.
- `tests/ProductEditSmoke/Program.cs`, `ProductEditSmoke.csproj`, `preview.cjs`; `tests/ProductSmoke/Program.cs` regression expectation.
- `docs/product-editing.md`, `catalogue-foundation.md`, `architecture.md`, `product-selling-options.md`, `design-system.md`, `product-management.md`.
