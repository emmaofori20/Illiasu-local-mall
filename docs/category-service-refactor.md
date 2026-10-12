# Authoritative Category Management service

Implemented 11 October 2026 following the service architecture review. This is a focused category refactor: no schema, migrations, Identity, routes, Razor views, JavaScript, CSS or unrelated modules changed.

## Responsibilities

`CategoryManagement` now normalizes names/slugs, evaluates required/length/slug-format rules and checks slug uniqueness before every create or edit. Names are trimmed and capped at 100 characters; slugs are trimmed, lowercased, capped at 160 characters and restricted to lowercase ASCII letters/digits with single internal hyphens. Inactive categories still reserve their slugs. Display names are not unique; this existing behaviour is intentionally retained.

`CategoryDetailsInput` holds shared validation metadata in the application module. `CategoryForm` inherits it, keeping the existing form metadata and client hints. The service invokes validation itself, so a direct service caller cannot bypass the rules by avoiding MVC ModelState. Validation does not truncate names/slugs or silently generate a different slug.

`ListAsync` returns a materialized `CategoryPage` DTO and owns search trimming/length limits, active/inactive filtering, deterministic name/ID ordering, linked-product counts and server pagination. Allowed sizes remain 10, 25, 50 and 100; invalid sizes fall back to 10, pages clamp to valid bounds and empty lists retain page one. Public IQueryable and the separate public slug-existence preflight method were removed. EF Core remains directly inside the concrete service.

Controllers retain binding, authorization, antiforgery, HTTP confirmation, signed-token parsing, ModelState translation, existing success messages, redirects and modal-compatible form responses. Raw Name/Slug metadata errors are discarded before invoking the service because normalization may make uppercase/padded input valid; other binding errors are retained. The normalized service values and its field errors are rendered using the unchanged views.

## Explicit outcomes and preservation

Create, Edit and ChangeStatus return `CategoryWriteResult`, with normalized Name/Slug, field errors and an optional current snapshot:

| Outcome | Meaning / HTTP presentation |
| --- | --- |
| Succeeded | Mutation applied; redirect with the existing success feedback. |
| ValidationFailed | Invalid name/slug or duplicate slug; existing form/modal shows field errors with entered normalized values retained. |
| NotFound | Target no longer exists; HTTP 404. |
| Conflict | Signed snapshot no longer matches; existing HTTP 409 review/reconfirmation response. |
| AlreadyApplied | Status already matches the requested result under the existing snapshot conditions; acknowledge without toggling back. |

Existing Data Protection purpose and snapshot payload are unchanged. Binary-exact name/slug plus activation predicates still guard edits; status also checks linked-product count. Replay acknowledgement now belongs to the service. The controller still requires explicit confirmation and refreshes the signed snapshot on conflicts using the same UI flow.

SQL unique/check constraints remain the final integrity protection. Preflight uniqueness is advisory under concurrent inserts/edits; SQL errors 2601/2627 are translated into a Slug validation outcome. A rejected inserted entity is detached so it cannot be inadvertently retried on a later save. Unexpected database failures are not converted into business-validation errors. Single SaveChanges and single conditional UPDATEs remain atomic; no redundant multi-command transaction was introduced.

Activation only changes Category.IsActive. Product links and product/variant flags are untouched; no hard deletion was introduced.

## Verification

- `CategoryServiceSmoke`: direct create/edit normalization; null/blank/oversized names and slugs; malformed slug shapes; exact length boundaries; own/other/inactive slug uniqueness; duplicate names allowed; no writes for rejected inputs; SQL insert/update uniqueness races; reusable context after failed insertion; signed-token tampering/identity; explicit not-found/conflict/already-applied outcomes; linked-product preservation/count conflicts; all pagination sizes, filters, bounds and no public IQueryable.
- Existing `CategorySmoke`: anonymous/customer/staff denial, Admin/InventoryManager access, antiforgery, overposting, validation, normalization, creation/editing, duplicate/inactive slugs, signed tamper/stale conflicts, activation/deactivation/replay, visibility warning/reference preservation, search/pagination/filtering and no delete route. The existing harness was unchanged.
- Application and both harness builds passed with zero warnings/errors. Both SQL-backed suites passed against guarded Development Emmanuel / IlliasuLocalMallDb and removed only their generated fixtures/accounts.

```powershell
dotnet build tests/CategoryServiceSmoke/CategoryServiceSmoke.csproj
dotnet run --no-build --project tests/CategoryServiceSmoke
# Run the MVC HTTPS development server before the HTTP suite.
dotnet run --no-build --project tests/CategorySmoke
dotnet build
```

No styling build was needed because Tailwind inputs and views were unchanged.

## Remaining limitations

Category concurrency remains value/signed-snapshot based, not rowversion based. Change-and-revert can match an older snapshot, and an unchanged product count does not prove identical linked products. This existing behaviour was expressly preserved; stronger revision protection requires a separately reviewed schema change. Query count/page reads are separate statements, so simultaneous changes can affect a page's reported total as before.

Direct SQL/bulk writes can bypass application validation; existing SQL constraints cover only their declared invariants, not the complete slug-format policy. Current MVC authorization remains the entry-point guard; future adapters must establish their own trusted permissions. Persistent administrator audit history and broader unavailable/retry outcomes remain separate work.

## Changed files

- `Application/Catalog/CategoryManagement.cs` and new `CategoryContracts.cs`.
- `Areas/Admin/Controllers/CategoriesController.cs` and `Areas/Admin/ViewModels/CategoryModels.cs`.
- New `tests/CategoryServiceSmoke/CategoryServiceSmoke.csproj` and `Program.cs`.
- This document and follow-up notes in `docs/category-management.md` and `docs/service-architecture-review.md`.
