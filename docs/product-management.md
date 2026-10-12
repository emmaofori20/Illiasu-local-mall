# Product Management listing

Implemented 11 October 2026 under the owner's explicit listing instruction. Reuses
Product, ProductVariant, Category, ProductImage and ApplicationDbContext. No entities,
packages, schema changes or migrations were added/applied. EF confirms no pending model changes.

## Scope and access

`/Admin/Products` is live and database-backed. Admin/InventoryManager may list products
and confirm activation changes; anonymous users, Customer and SalesStaff are denied.
Other Admin permissions and the Development-only administration boundary remain.

Add product opens the functional [creation workflow](product-creation.md); Edit opens the dedicated
[editing workflow](product-editing.md) with immutable existing stock rules and rowversion protection.
No delete, inventory deductions, supplier/purchasing/checkout/payment operations exist.

## Queries and display rules

- Three async, untracked SQL projections: filtered count, ordered/paged rows and
  category choices. No Include graphs, per-row queries or in-memory filtering.
- Product-name or any variant SKU substring search, category and Product.IsActive
  filters combine. Search is trimmed/capped at 100 characters and parameterized.
  Inactive categories remain filterable. Malformed typed parameters return 400 with
  filter errors. Unknown statuses normalize to all.
- Sizes: 10 (default), 25, 50, 100. Unsupported sizes normalize to ten; pages clamp.
  Ordering is name then ID. All numbered/arrow links and size changes retain filters.
- Variant counts and GH₵ ranges include all variants, including inactive/unavailable.
  Zero variants show `No prices yet`, not fabricated prices. Product status does not
  imply available stock; inactive parent categories have a separate warning.
- Thumbnails prefer general product images, then primary/display order/ID; variant
  imagery is fallback. Only safe local root-relative paths are rendered. Unsafe or
  missing images use an accessible fallback. Images reserve 48px square, contain fit,
  lazy loading and async decoding. No assets were added.

## Status lifecycle

GET Status signs an existing-rowversion snapshot. POST requires roles, antiforgery
and explicit confirmation. A single conditional SQL update checks rowversion,
activation and parent-category activation, changing only Product.IsActive. SQL Server
updates the existing rowversion. Stale forms return 409 with refreshed confirmation;
tampered snapshots fail. Repeating an already-applied status never toggles it back.

Variants, their flags/prices, images and catalogue references remain intact.
Future storefront queries must exclude inactive products and respect category and
variant eligibility. Reactivation does not override those rules. The demonstration
storefront and dashboard fixtures remain unchanged.

## Presentation and states

Inter, Fresh Market semantic tokens, the admin shell, popover styles and dialog shell
are reused. Category and Product pages share `_AdminPagination` and the progressive
catalogue interaction script. Each product has Edit and Activate/Deactivate actions;
only status uses a working confirmation dialog in this slice.

Search loading, empty results, validation, safe database-unavailable feedback/retry,
missing images and success states are included. Tables scroll internally on mobile.
Menus preserve keyboard navigation; confirmation supports Escape, focus containment
and restoration to the visible row Actions button.

## Changed files

- `Application/Catalog/ProductManagement.cs`, `ProductListing.cs` (read models)
- `Areas/Admin/Controllers/ProductsController.cs`, `ViewModels/ProductModels.cs`
- `Areas/Admin/Views/Products/Index.cshtml`, `_Actions.cshtml`, `Status.cshtml`, `ComingSoon.cshtml`
- Shared `_AdminPagination.cshtml`, `_AdminLayout.cshtml`, `_AdminNavigation.cshtml`, admin `_ViewImports.cshtml`
- Category `Index.cshtml`, `_Pagination.cshtml` (shared component integration)
- `Program.cs`, `Infrastructure/Identity/AppRoles.cs`, `AdminAreaAuthorizationConvention.cs`
- `wwwroot/js/admin-categories.js`, generated `wwwroot/css/app.css`
- `tests/ProductSmoke/ProductSmoke.csproj`, `Program.cs`, `preview.cjs`
- This document, catalogue-foundation.md, architecture.md and design-system.md

## Verification

Tailwind production and .NET builds pass with zero .NET warnings/errors. EF reports
no model changes since the existing migration. ProductSmoke passes for query count/no
tracking, aggregates, empty variants, SKU/name search, combined filters, all sizes,
boundaries, safe parameters, roles, antiforgery, confirmation, tampered/stale snapshots,
duplicate status requests and preservation of variants/images. CategorySmoke also passes.

Authenticated Edge checks and visual screenshot review passed at 375/768/1440px for
listing, menus, confirmation, empty results and holding pages, with Inter and no page
overflow. Numbered navigation, all-filter retention, modal Escape/focus restoration
and status-save refresh passed. Generated QA fixtures and accounts were removed.
Existing Identity tables, users and business records were preserved.

```powershell
npm.cmd run css:prod
dotnet build
# Build the harness before starting MVC, to avoid Windows apphost locks:
dotnet build tests/ProductSmoke/ProductSmoke.csproj
# With Development running at https://localhost:7116:
dotnet run --no-build --project tests/ProductSmoke/ProductSmoke.csproj
```

Optional `--preview` retains disposable fixtures for at most ten minutes and writes
temporary session cookies only to OS temp storage. With headless Edge CDP on 9225,
`node tests/ProductSmoke/preview.cjs` verifies the UI and signals fixture cleanup.

The dedicated product/variant creation workflow is now implemented. Next: editing and approved image
storage/upload rules. Remote image hosting is not enabled by this slice.
