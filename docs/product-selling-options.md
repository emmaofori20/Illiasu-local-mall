# Staff-friendly Add Product and stock behaviour

Implemented using the existing VariantStockMappings schema; no migration, new table,
package, inventory balance, deduction, reservation, purchasing or payment functionality.
The existing Inter/Tailwind v4 green-and-white two-column page and admin policies remain.

## Form and stock rules

Selling options use Size / Packaging option, Product code (SKU), optional Barcode, Selling unit,
independently entered Selling price (GH₵), Active and Manually available. The default is
Count this option separately. The server derives a self-owned mapping with factor one,
regardless of a submitted unused factor/source. Sealed cartons and 5 kg/25 kg rice bags
therefore remain distinct unless sharing is explicitly selected.

Use stock from another option reveals a separately counted source and explicit consumption.
Units per pack is used for discrete packs/cartons; measured/other contexts say Stock quantity
per sale. Native unit precision informs the numeric increment, while server validation remains
authoritative. No pack quantity or carton price is inferred. Live text explains the selected
source, sold unit and quantity, and explicitly notes that inventory is not enabled yet.
Source choices show option name and product code to distinguish similar names.

Advanced details retain selling increments, fractional measured quantities and optional
receiving/counting conversions. Those conversions do not create a selling price, share a
pool or unpack goods. They must normalize into representable base-stock quantities too.

## Stable references and atomic persistence

Each VariantCreateInput has a unique ClientRowKey, StockMode (Own/Shared), optional
StockOwnerRowKey and nullable UnitsPerSale, alongside existing input fields. Collection
indices remain only MVC binding positions. JavaScript generates a new UUID for each added
row; existing keys survive validation and unrelated row removal. Removing a referenced source
or changing it to Shared is blocked with explanatory feedback. Source references are never
silently reassigned; invalid prior selections remain visible for correction.
Changing a source, its unit or a shared option's sold unit clears its dependent consumption
quantity with a review message. Staff must re-enter it; no old factor silently acquires new units.

The creation service resolves keys only inside the submitted new product, validates distinct
nonempty keys and direct independently stocked roots, and generates persistent variant IDs.
Submitted persistent product/stock-owner IDs are ignored. The shared Domain StockConsumptionRules
is reused by ProductCreation and the existing DbContext mapping validator for positive
decimal(18,6), base-unit/fractional precision and normalized quantity increments. Missing owners,
self-sharing, chains, cycles, unknown modes and duplicate keys fail before writes.

A single Serializable transaction saves Product, all selling variants, explicit mappings and
optional conversions. Each price remains independently entered. Existing global SKU/barcode
uniqueness, authorization, antiforgery, image gate and SQL error handling remain authoritative.

## Accessibility and verification

Native radios and selects support keyboard use. Labels/error descriptions remain linked after
row/conversion renumbering; radio IDs stay unique. Stock summaries use polite live regions.
Validation retains values, modes, source keys and quantities, focuses the error summary and
provides links to invalid fields. Hidden Advanced sections open before invalid-field focus.
With JavaScript disabled the first independent option remains submit-capable; dynamic multiple
options and live stock summaries require JavaScript. No SPA framework was added.

SQL/HTTP tests pass for single items, reordered shared cartons (12 bottle units), independent
sealed cartons, independent rice bags, measured kg consumption, independent carton prices,
duplicates, reference injection, missing/invalid owners, missing/zero/negative/fractional/excess-
precision quantities, receiving normalization, duplicate keys, chains/cycles, antiforgery and
Customer/SalesStaff denial. Forced failure before commit rolls back creation. Existing mapping,
catalogue and product-listing regressions pass. Fixtures/accounts are removed after testing.

Browser tests and screenshot inspection at 375, 768 and 1440px verify responsive fit, Inter,
live summaries, root protection, stable links after earlier-row removal, label/ARIA references,
unique IDs, keyboard stock selection, three-option saving, duplicate/mapping validation and
field focus. A subsequent SQL query verifies the browser-created shared/sealed mappings and
independently entered prices before cleanup. Tailwind and .NET builds pass.

## Changed files and remaining boundaries

- Application/Catalog/ProductCreateInput.cs and ProductCreation.cs.
- Domain/StockConsumptionRules.cs; Infrastructure/Persistence/VariantStockMappingValidator.cs
  (shared rule extraction; no EF model change).
- Areas/Admin/Controllers/ProductsController.cs; Views/Products/Create.cshtml,
  _CreateVariant.cshtml, _CreateConversion.cshtml and new _StockBehaviour.cshtml.
- wwwroot/js/product-create.js and generated wwwroot/css/app.css.
- tests/ProductCreateSmoke/Program.cs, preview.cjs and CatalogueSmoke's zero-increment SQL check.
- This document and related architecture/design/catalogue documentation.

Image previews remain available, but uploads still fail closed pending durable storage and
decoded-image validation. Editing existing mappings still requires a reviewed revision workflow.
Stock summaries describe future consumption rules; no actual stock balances are read or changed.
Active categories and unit definitions must exist. No schema gaps were found.

Existing options can now be maintained through [Product editing](product-editing.md). Existing stock mappings and quantity rules remain read-only; new options reuse this workflow and can target existing independent roots.
