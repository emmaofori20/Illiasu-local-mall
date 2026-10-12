# Category Management

The subsequent [authoritative service refactor](category-service-refactor.md) moves
normalization, validation, uniqueness and listing queries into CategoryManagement,
with explicit outcomes and direct service tests. Existing routes, modal responses,
signed concurrency and presentation are preserved.

Implemented under the owner's explicit Category Management instruction, 11 October 2026.
Uses the existing Category entity and ApplicationDbContext. No entities, packages,
schema changes or migrations were added. Identity and other catalogue tables are unchanged.

## Access and operations

- `/Admin/Categories`: live records, name/slug search, active/inactive filtering and
  stable pagination with 10 (default), 25, 50 or 100 rows. Create, edit and confirmed
  activation/deactivation are implemented.
- Admin and InventoryManager can access all category actions. Other Admin controllers
  remain Admin-only. Navigation reflects those permissions; server authorization is authoritative.
- Existing Development-only administration routing is retained, with an independent
  controller environment guard. Production rollout is not enabled by this slice.
- All mutations are POST with antiforgery validation. Bind allowlists prevent activation,
  identifiers and product collections being modified through the detail form.
- Names are trimmed, required and limited to 100 characters. Slugs are trimmed,
  lowercased, limited to 160 characters and restricted to letters, digits and single
  hyphens between words. SQL's existing unique slug index protects concurrent submissions,
  including inactive categories; duplicate errors are presented on the field.
- Signed form snapshots plus conditional SQL updates reject stale edits/status changes.
  Status confirmation also checks the linked-product count. Repeating a just-applied
  status request does not toggle it back. These snapshots detect changed values, not a
  permanent revision history; a full audit trail/version counter is a separate decision.

## Visibility and preservation

Deactivation changes only Category.IsActive. Product references, product/variant flags
and historical identifiers remain intact. There is no delete endpoint or global query
filter. The admin list continues to show inactive categories.

Future data-driven storefront browsing, search and ordering must require an active category,
active product and active/available variant. An inactive category excludes its linked products
from those customer journeys. Reactivation does not override individual product availability.
The existing demonstration storefront and dashboard metrics are not connected to category data
and are unchanged by these operations. Forms explain this distinction before confirmation.

## Presentation and verification

Create/Edit and status links now progressively open within the Categories listing.
The shared `_AdminFormDialog` reuses the existing full-page response's form/confirmation
section; no controller, service, authorization or schema changes were necessary.
Forms POST with their existing antiforgery tokens. Server-rendered validation and
409 responses replace the dialog body while retaining input and refreshed snapshots.
Success replaces the listing with the redirected MVC response and focuses its success
notice. Filters reset to the server's default list after saving. Network/session errors
retain inputs; an uncertain request advises checking the listing before retrying.

Creation generates an ASCII slug from the name until the explicit Edit slug control is
used. Name changes during editing never regenerate the existing slug. Without JavaScript,
links/forms remain functional full-page MVC flows and slugs can be entered directly.
The modal supports initial focus, explicit forward/backward Tab trapping, Escape and
opener focus restoration. Native dialog support is required for enhancement; otherwise
ordinary links remain available.

Modal refactor changed files: `Areas/Admin/Views/Categories/Index.cshtml`, `Form.cshtml`,
`_Fields.cshtml`, `Status.cshtml`, `Areas/Admin/Views/Shared/_AdminFormDialog.cshtml`,
`wwwroot/js/admin-categories.js`, `Styles/components.css`, generated `wwwroot/css/app.css`,
`tests/CategorySmoke/Program.cs`, `tests/CategorySmoke/modal-preview.cjs`, this document
and `docs/design-system.md`. Modal browser tests verify responsive geometry,
initial/restored focus, slug generation/override, duplicate validation with retained
values, prefilled editing, stable existing slugs, save feedback, both status transitions
and keyboard focus containment. Run the new script instead of `preview.cjs` for these
flows while `CategorySmoke --preview` holds its temporary fixtures. It signals cleanup
on success; fixture prefixes also cover categories created by the modal test.

Inter, Fresh Market semantic tokens and the existing Wharf-inspired admin shell are reused.
Shared fields, native browser constraints, server validation summaries, success feedback,
submission loading states, empty states and explicit confirmation support keyboard use.
The table has a labelled scroll region and mobile overflow hint. All input controls are at
least 44px high. The existing drawer preserves focus and closes with Escape.

```powershell
npm.cmd run css:prod
dotnet build
# Start the application in Development at https://localhost:7116, then:
dotnet run --project tests/CategorySmoke/CategorySmoke.csproj
```

Stop the MVC process before rebuilding to avoid Windows apphost file locks. Alternatively
build the smoke project first and run it with `--no-build` while the app is running.
Tests guard the Windows-authenticated local Emmanuel / IlliasuLocalMallDb target and remove
only their generated fixtures. They cover authorization for every category action,
antiforgery, required/length/slug validation, normalization, unique active/inactive slugs,
editing, stale/tampered forms, product preservation, repeated status submissions,
reactivation, search, pagination/page-size validation, empty results and absence of permanent deletion.

The listing's Add category action includes a plus icon. Each row has a vertical
three-dot Actions button opening a native top-layer popover with only Edit and
Activate/Deactivate. Menus support Arrow keys, Home/End, Escape with trigger focus
restoration, Tab exit and outside-click dismissal. Dialog focus returns to the row's
visible Actions button, rather than its hidden menu item. Popovers reposition on
scroll and close on resize; they are not clipped by the table's scroll region.

Pagination displays result ranges, numbered links with the current page announced,
previous/next arrows and a labelled size selector. All page/size requests retain
search and status. Changing size starts at page one; unsupported sizes default to ten.
Large page counts use a bounded set of numbers and ellipses. A native Apply button
remains available without JavaScript; enhancement submits on selection changes.
The reusable Razor partials are `_Actions.cshtml` and `_Pagination.cshtml`.

Responsive Edge checks passed for listing, create, edit, confirmation and empty results
at 375px, 768px and 1440px, with no page-level horizontal overflow. Mobile drawer opening,
Escape dismissal and focus restoration passed. Screenshots were visually reviewed.
Optional `CategorySmoke --preview` prepares a temporary authenticated fixture session;
`node tests/CategorySmoke/preview.cjs` checks a separately launched local Edge CDP endpoint
on port 9225. Temporary session cookies are stored only in the OS temp directory and
removed when preview completes or times out after ten minutes.

## Outstanding business decisions

1. Display-name uniqueness: currently duplicate names are allowed, consistent with the
   existing schema; slugs are unique. A stricter name rule needs approval and a schema review.
2. Category slug changes are allowed. Decide whether future published URLs should freeze
   slugs or maintain redirects before integrating the customer catalogue.
3. Categories remain flat. Hierarchy, ordering, category imagery and bulk actions are
   outside this slice and require separately approved requirements.
4. Confirm production rollout and any requirement for a permanent category change audit
   history. Product reassignment/CRUD and storefront integration remain separate slices.

## Changed files

- `Areas/Admin/Controllers/CategoriesController.cs`
- `Application/Catalog/CategoryManagement.cs`
- `Areas/Admin/ViewModels/CategoryModels.cs`
- `Areas/Admin/Views/Categories/Index.cshtml`, `Form.cshtml`, `_Fields.cshtml`, `Status.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`, `_AdminNavigation.cshtml`
- `Infrastructure/Identity/AppRoles.cs`, `AdminAreaAuthorizationConvention.cs`, `Program.cs`
- `Styles/components.css`, generated `wwwroot/css/app.css`, `wwwroot/js/admin-categories.js`
- `tests/CategorySmoke/CategorySmoke.csproj`, `Program.cs`, `preview.cjs`
- `docs/category-management.md`, `docs/catalogue-foundation.md`, `docs/design-system.md`

Final Tailwind and .NET builds passed with zero .NET warnings/errors. The SQL-backed
CategorySmoke and existing IdentitySmoke suites passed. Temporary fixtures were removed,
and the existing administrator and Identity tables were preserved.
