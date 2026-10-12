# Service architecture review

Subsequent implementation: [Category service refactor](category-service-refactor.md)
addresses authoritative category validation, public IQueryable exposure and category
write outcomes. The dated findings below remain the audit snapshot; other recommended
refactors were not implemented by that follow-up.

Reviewed 11 October 2026. **Audit only:** this document is the only source change. No controllers, services, entities, migrations, Identity, Razor or styling were changed. No database commands, provisioning commands or database-writing tests were executed for this review.

## Assessment and evidence

The application is a single-project .NET 8 MVC modular monolith with SQL Server, EF Core 8.0.31, standard ASP.NET Core Identity and an Admin Area. Four implemented application services handle catalogue administration. Product editing already exists; it is not a missing backend workflow. Inventory, Suppliers, Purchasing, Orders and Payments remain proposed modules, not implemented services. Dashboard sample records are presentation fixtures, not persisted business modules.

The architecture is appropriate for the current scale. Keep direct EF Core access inside focused application services. The priorities are consistent command validation, smaller controller/query responsibilities, explicit concurrency/error contracts, and controlled persistence entry points before additional writers are introduced. Generic repositories, a generic service framework, mandatory interfaces for every service and separate deployable services would add little value here.

Sources inspected: [AGENTS.md](../AGENTS.md), [architecture.md](architecture.md), [Program.cs](../Program.cs), all five controllers, every file in `Application/Catalog`, all seven catalogue entities, Identity infrastructure, DbContext/factory, Fluent configurations, mapping validator, unit seeding, image boundary and existing smoke-test source. Historical sections of architecture.md describe earlier phases; current source and its implemented-slice notes establish implementation status.

## 1. Implemented service inventory

| Application service | Implemented responsibility | Dependencies and write boundary |
| --- | --- | --- |
| [CategoryManagement](../Application/Catalog/CategoryManagement.cs) | Exposes an untracked category query; finds snapshots; signs/reads protected snapshots; checks slug existence; creates, edits and toggles category activation. | Scoped DbContext and `IDataProtectionProvider`. Creation uses tracked SaveChanges; edit/status use conditional ExecuteUpdate. Normalization and most validation remain in the controller. |
| [ProductManagement](../Application/Catalog/ProductManagement.cs) | Efficient listing/search/category/status filters and pagination; SQL variant counts/price ranges/thumbnail selection; finds/signs/reads status snapshots; changes product activation; checks safe local image paths. | Scoped DbContext and `IDataProtectionProvider`. Three listing projections; conditional status update includes product rowversion and category activation. SafeImagePath is a presentation/storage safety helper, not a catalogue command. |
| [ProductCreation](../Application/Catalog/ProductCreation.cs) | Normalizes inputs; validates product, selling options, identifiers, units, quantity precision, receiving conversions and direct stock ownership; saves a new aggregate. Its internal ValidateCatalogueAsync also serves editing. | Scoped DbContext. One Serializable transaction saves product, variants, conversions and explicit mappings. SQL constraints handle identifier races; mapping validation runs during SaveChanges. |
| [ProductEditing](../Application/Catalog/ProductEditing.cs) | Loads authoritative existing identities/mappings; validates complete existing-option membership and product/variant versions; changes permitted fields; adds new independent/shared options; preserves existing SKU/unit/stock rules. | Scoped DbContext and ProductCreation. One Serializable transaction. Submitted original rowversions protect UPDATEs; every edit advances the product token, including option-only changes. |

`ProductListing.cs`, `ProductCreateInput.cs` and `ProductEditInput.cs` contain DTOs/inputs/results, not additional services. There are no other implemented application-service classes under `Application`.

Related implementations are distinct from application use cases:

| Component | Responsibility and registration |
| --- | --- |
| [StockConsumptionRules](../Domain/StockConsumptionRules.cs) | Pure shared domain validation: positive decimal(18,6) consumption, base-unit precision, discrete/fractional rules, overflow and representable normalized quantities. Static; no DI registration required. |
| [VariantStockMappingValidator](../Infrastructure/Persistence/VariantStockMappingValidator.cs) | Tracked SaveChanges safeguard: immutability checks, default self mappings, same-product/unit references, direct independently stocked roots and consumption validation. Internal static; invoked by DbContext, not a DI service. |
| [ApplicationDbContext](../Infrastructure/Persistence/ApplicationDbContext.cs) | Identity plus seven catalogue DbSets; applies Fluent configurations; coordinates mapping validation and owned/caller transactions. Scoped through AddDbContext. |
| [UnitReferenceData](../Infrastructure/Persistence/UnitReferenceData.cs) | Explicit add-only/idempotent reference seeding; Serializable transaction and SQL application lock. Static, called by the guarded development CLI with a scoped context. No ordinary startup/page-load seeding. |
| [IdentityProvisioning](../Infrastructure/Identity/IdentityProvisioning.cs) | Local-development target guard, idempotent four-role seeding, locked first-admin provisioning with private credentials. Static CLI utility, resolving scoped Identity managers. |
| UserManager / SignInManager / RoleManager | Framework Identity services for user/role persistence, password validation/hash checking, lockout and cookie sign-in/out. These are implemented framework dependencies, not missing custom user repositories. |
| [IProductImageUploadBoundary / DisabledProductImageUploadBoundary](../Infrastructure/Storage/ProductImageUploadBoundary.cs) | Existing external-storage seam currently rejects all attempted image saves; performs candidate size/type/primary checks. Stateless singleton. It is not durable storage or decoded-image verification. |
| [DashboardPrototype](../Areas/Admin/ViewModels/DashboardPrototype.cs) | Deterministic sample dashboard composition. Static presentation factory, not an inventory/reporting service. |
| ApplicationDbContextFactory / AdminAreaAuthorizationConvention | Design-time configuration and MVC authorization metadata respectively; not application use cases. |

The seven catalogue entities are Category, Product, ProductVariant, UnitOfMeasure, ProductUnitConversion, ProductImage and VariantStockMapping. They are mostly mutable persistence models; the domain-rule extraction is limited to StockConsumptionRules. ApplicationUser extends IdentityUser without replacing Identity tables.

## 2. Complete MVC action map

The rows below cover every current MVC action. Private helpers and OnActionExecuting hooks are described separately; they are not routes.

GET labels describe the ordinary page flow. Index/display actions without an explicit HttpGet attribute are not verb-restricted in source; they remain non-mutating and unsafe-verb requests still encounter the global antiforgery filter. All implemented browser write actions explicitly require POST.

### CategoriesController

All actions require Admin or InventoryManager through CategoryManagement policy and remain Development-only.

| Action | Invoked service / EF operation | Boundary observation |
| --- | --- | --- |
| Index GET | CategoryManagement.Query; controller composes filters, CountAsync, ordering, Skip/Take and snapshot projection. | The service leaks IQueryable; controller owns pagination/query execution. |
| Create GET | No service/database operation; new CategoryForm. | Presentation only. |
| Create POST | Controller Normalize/TryValidateModel; SlugExistsAsync; CreateAsync → Categories.Add + SaveChangesAsync. | Controller owns business input validation/uniqueness feedback; database unique index covers races. |
| Edit GET | FindAsync; Form helper → Sign. | Loads a signed category snapshot. |
| Edit POST | FindAsync; Read; Normalize; SlugExistsAsync; EditAsync → value-conditioned ExecuteUpdate; FindAsync/Sign on conflict. | Snapshot checks and conflict presentation split between controller/service. |
| Status GET | FindAsync + Sign. | Confirmation data only. |
| Status POST | FindAsync + Read; controller checks confirmation and already-applied status; ChangeStatusAsync → conditional ExecuteUpdate; FindAsync/Sign on conflict. | Product-count condition preserves the reviewed visibility summary; no deletion or link rewriting. |

OnActionExecuting provides the Development restriction and live-page flag. Normalize, Form, EditConflict and Unique are private helpers.

### ProductsController

All actions require Admin or InventoryManager through ProductManagement policy and remain Development-only.

| Action | Invoked service / EF operation | Boundary observation |
| --- | --- | --- |
| Index GET | ProductManagement.ListAsync. | Controller maps malformed filters and SQL failure to HTTP/UI states. Service owns query execution. |
| Create GET | Populate directly queries active Categories and UnitsOfMeasure with AsNoTracking/ToListAsync. | No write; form lookup eligibility should have a query boundary. |
| Create POST | ProductCreation.Normalize; MVC/request-field checks; image boundary Validate; ProductCreation.CreateAsync; Populate on errors. | All product writes are in the service. Field-presence checks and ModelState translation are HTTP concerns. |
| Edit GET | Direct Products.AsNoTracking().Include(Variants).SingleOrDefaultAsync; PopulateEdit directly queries categories, units, variants with units, and mappings. | Controller assembles the edit snapshot and eligible stock-source options. |
| Edit POST | Direct product slug/category lookup; Normalize/MVC/request-field checks; ProductEditing.EditAsync; PopulateEdit on errors. | Route/body identity check is appropriate in MVC. Immutable-value reconstruction and authoritative write checks also run inside ProductEditing. |
| Status GET | ProductManagement.FindAsync + Sign. | Confirmation data only. |
| Status POST | FindAsync + Read; controller checks confirmation and already-applied status; ChangeStatusAsync → rowversion-conditioned ExecuteUpdate; FindAsync/Sign on conflict. | Idempotent acknowledgement logic is in the controller; no unconditional toggle write. |

Populate and PopulateEdit perform direct **reads**, not controller-owned product writes. PopulateEdit also derives independent root eligibility from mappings and returns EF entities through presentation models; use a focused read DTO/query instead. OnActionExecuting provides the Development restriction/live flag.

### AccountController

| Action | Invoked service / EF operation | Access and responsibility |
| --- | --- | --- |
| Index GET | None; View. | Authorized account page. |
| Register GET | None; RegisterViewModel. | Anonymous entry. |
| Register POST | DbContext.BeginTransactionAsync; UserManager.CreateAsync; AddToRoleAsync(Customer); commit; SignInManager.SignInAsync. | Anonymous, rate-limited. User creation and Customer-only role assignment are atomic; the controller owns this application workflow. |
| Login GET | None; LoginViewModel. | Anonymous entry. |
| Login POST | UserManager.FindByEmailAsync; SignInManager.PasswordSignInAsync with lockoutOnFailure. | Anonymous, rate-limited. Identity owns credential/lockout logic; controller provides generic feedback. |
| Logout GET | None; confirmation view. | Authorized; does not sign out. |
| LogoutConfirmed POST, route action name Logout | SignInManager.SignOutAsync. | Authorized mutation, global antiforgery filter. |
| AccessDenied | None; sets HTTP 403, View. | AllowAnonymous; no database mutation. No explicit verb attribute. |

SafeRedirect validates local return URLs and should stay at the HTTP boundary. Login/logout can continue using Identity managers directly; a custom wrapper adds value only where it owns an actual workflow. Registration orchestration is a clear candidate for a concrete application use case.

### Other controllers

| Controller/action | Invocation | Status |
| --- | --- | --- |
| Home.Index | View only. | Storefront presentation; no persisted catalogue/cart query. |
| Home.Privacy | View only. | Static presentation. |
| Home.Error | ErrorViewModel with Activity/HTTP trace ID. | HTTP concern; no service/database operation. |
| Admin Overview.Index | DashboardPrototype.Create(period). | Admin-only, Development-only sample dashboard; no SQL reporting/inventory service. |

## 3. Consistency review

### Authorization and HTTP safety

Server-side access control is present. Program registers AdminArea, CategoryManagement and ProductManagement policies; both catalogue policies allow Admin/InventoryManager. The AdminArea convention applies a deny-anonymous/default Admin filter to area controllers and special-cases the two staff-accessible controllers. Overview also explicitly requires Admin. Authentication/authorization middleware is correctly ordered after routing; cookies are secure/HttpOnly, with SameSite policies, password rules and lockout. Public registration assigns Customer only.

Services themselves do not inspect the caller's roles. That is acceptable for today's authorized MVC entry points, but future jobs/imports/POS adapters must have an explicit trusted caller/actor and appropriate permission checks. Do not assume an internal service is safe for any future external caller merely because its current controller is authorized. The convention's type-specific exceptions will otherwise add default Admin authorization to a future InventoryManager controller as well, effectively requiring both policies. Make policy selection extensible while retaining a secure default and the prohibition on AllowAnonymous.

Global AutoValidateAntiforgeryToken protects browser mutation requests. Catalogue POSTs also declare ValidateAntiForgeryToken. Account POSTs rely on the global filter; this is not a missing protection. Product forms use narrow bindings, typed inputs and request limits. Mapping/immutable fields are not editable existing-option inputs. No delete endpoint exists.

### Writes and transactions

| Write path | Current guarantee | Limitation / recommendation |
| --- | --- | --- |
| Category creation | One tracked SaveChanges with database uniqueness/checks. | A single SaveChanges does not need a redundant explicit transaction. Move input/business validation into its command service. |
| Category edit/status | One conditional SQL statement; atomic as a statement. | ExecuteUpdate bypasses tracking validators and automatic concurrency checks; current predicates intentionally affect only category fields. Keep this allowlist explicit. |
| Product activation | One conditional SQL statement with product version/state and category activation. | Atomic statement; no multi-write transaction needed. Already-applied detection is outside this service. |
| Product creation/editing | Serializable validation and all aggregate writes in one transaction. | Correct foundation; locks can contend. Do not add automatic retry of an uncertain commit without operation identity/reconciliation. |
| Public registration | Identity create + Customer membership share the same scoped EF context and explicit transaction; cookie sign-in occurs after commit. | Move orchestration, not password/cookie internals, into a registration use case. Pass cancellation where the framework supports it. |
| First-admin provisioning / unit seeding | Guarded explicit commands; transaction-owned SQL application locks. | No automatic migrations or production setup; retain this boundary. Unit seeding preserves conflicting/inactive existing definitions rather than overwriting them. |

There is no persistent administrator change-audit service, commerce idempotency store, inbox/outbox or stock transaction ledger. Signed status confirmations and reference-data seeding are narrow forms of repeat safety, not a durable payment/inventory idempotency implementation.

### Validation

ProductCreation/Editing enforce authoritative catalogue validation independently of ModelState. They validate identifier uniqueness against inactive records too, explicit units/conversions, price precision and direct ownership. SQL unique indexes/FKs/checks provide a final race/integrity barrier. New options need active units; editing can retain historical inactive units and an unchanged inactive category.

CategoryManagement's public CreateAsync/EditAsync accept strings without the controller's trimming, length/slug-format validation or friendly uniqueness handling. SQL checks reject blanks and duplicates but do not express every MVC rule. A future caller can therefore bypass the intended category policy. This is the most concrete service-boundary gap: make category command validation authoritative in the service while keeping MVC binding/ModelState/confirmation rendering in the controller.

ProductEditing depends on ProductCreation for normalization and an internal validation method, reconstructs ProductCreateInput for existing rows, and maps indexed error names back into the edit form. This works but couples editing to another command's input/result shape. Extract a focused concrete catalogue validator/shared input rules when refactoring is approved; preserve the existing pure StockConsumptionRules and persistence safeguard. Use typed failure codes rather than interpreting user-facing strings. Explicit missing numeric-field checks currently exist only at the HTTP layer because DTO numeric defaults cannot distinguish omitted fields; define that distinction in future API contracts.

### Concurrency and failures

Products and variants have real SQL rowversion columns. ProductEditing checks the complete submitted existing-option set and all versions, then uses original versions in tracked updates. The aggregate product update is forced even for option-only edits. ProductManagement activation uses an explicit rowversion predicate. These are sound current implementations.

Category has **no rowversion column**. Its signed snapshot checks binary-exact name/slug and activation; status also checks linked product count. This detects ordinary conflicting value changes but not change-and-revert (ABA): a stale snapshot can match again after fields revert. Equal counts also do not prove identical linked products. This does not delete references, but it is weaker than version-based concurrency. Recommend an explicit category revision/rowversion in a separately approved additive schema task; do not describe it as an existing field or alter it in this audit.

Category/status conflicts produce HTTP 409 and may refresh the protected confirmation token for review; product-edit conflicts retain old versions and return the form with errors, ordinarily HTTP 200. Standardize explicit Conflict/NotFound/Validation/Unavailable outcomes and the deliberate reload/reconfirm UX. Never silently retry a stale user's intended update.

Product listing has an explicit SQL-unavailable 503 state. Category listing and many GET/status/lookup queries do not have an equivalent recoverable handling path. Creation/editing translate broad SQL-backed DbUpdateException cases into catalogue-constraint messages; a wrapped deadlock or connection fault can be misclassified. Direct deadlock SqlException has a separate branch. Catch known conflict/constraint cases narrowly, preserve useful diagnostic codes without PII, and avoid retrying a failed form-population query on the same outage without a fallback. Unexpected failures should remain logged/server errors rather than masquerading as validation errors.

## 4. Stock mapping safeguards and limits

The current mapping is explicit and variant-level: independently priced selling options map directly to a same-product stock owner. Self-owned consumption is one; shared consumption is a positive explicit factor. No packaging quantities or stock pooling are inferred.

Fluent configuration enforces mapping uniqueness, composite same-product/unit foreign keys, positive consumption, valid self-unit/factor rules and non-cascading references. SaveChanges Prepare rejects modification/deletion of existing mappings and changes to variant quantity rules or unit precision/dimensions. Added variants receive a default self mapping if no explicit mapping was supplied. ValidateAsync merges pending/persisted records, checks direct self-owned roots and calls StockConsumptionRules; cycles/chains fail.

DbContext creates a Serializable transaction when mapping inserts have no caller transaction. Shared inserts reject a weaker caller transaction. Creation/editing already supply Serializable. The synchronous SaveChanges path delegates to the asynchronous safeguard; ordinary callers should use async consistently.

This is a **tracked application-write safeguard**, not a complete SQL-enforced domain model. ExecuteUpdate/ExecuteDelete/raw SQL bypass it; SQL cannot by itself require every variant to have a child mapping or reject every cross-row ownership chain. Existing SKU immutability is enforced by the edit use case, not by a universal SaveChanges SKU check. Existing receiving conversions are read-only in Edit, but the mapping validator does not comprehensively validate or forbid every tracked conversion change. Ordinary catalogue services run the additional rules; arbitrary future writers must do so too. Restrict bulk/direct writes to reviewed, explicit operations and add regression tests for each permitted path before imports/POS are introduced. Any broader DB permissions/trigger policy needs separate review.

Mapping validation is not an inventory balance service. It does not reserve stock, prevent overselling, calculate costing or process payments. Preserve the mapping safeguards while designing those services; they cannot replace transactional balance guards and operation deduplication.

## 5. Dependency injection verification

Verified registrations against actual constructors in Program.cs:

| Registration | Lifetime / dependency check | Finding |
| --- | --- | --- |
| ApplicationDbContext / SQL Server | Scoped default; configuration reads ConnectionStrings:DefaultConnection. | Correct; services in a request share the same context. |
| CategoryManagement, ProductManagement | Scoped; context plus IDataProtectionProvider from framework MVC/Identity infrastructure. | Correct. |
| ProductCreation | Scoped; context. | Correct. |
| ProductEditing | Scoped; context plus scoped ProductCreation. | Correct; editing registration is present. |
| IProductImageUploadBoundary → DisabledProductImageUploadBoundary | Singleton with no captured scoped dependencies. | Correct for the current stateless fail-closed implementation. Revisit lifetime if replaced. |
| AddIdentity<ApplicationUser, IdentityRole> + EF stores/token providers | Framework managers/stores use the registered context; cookie/options/policies are configured. | Correct; no custom Users/Roles stores. |
| IdentityProvisioning, UnitReferenceData, StockConsumptionRules, mapping validator | Static helpers, called explicitly. | No missing DI registration. |
| ILogger, environment, configuration, authorization, antiforgery | Framework services. | Controller dependencies are covered. |

No missing registration or singleton-to-scoped lifetime conflict was found in source. This is registration/constructor inspection, not a new live container/database smoke test. Future background workers must create a scope per operation rather than capture DbContext in a singleton. No provider HTTP client, hosted commerce worker or external storage adapter is currently registered because those capabilities are absent.

## 6. Recommended modular organization and genuine interfaces

Retain one project, one database and folder-based modules. Keep the four existing concrete services initially; improve boundaries incrementally, without wholesale renaming or introducing a mediator solely for ceremony.

```text
Application/
  Catalog/
    Categories/       Category commands + listing/snapshot DTO queries
    Products/         Product creation/editing/status + form/list queries
    Validation/       Shared concrete catalogue validation
  Accounts/           Registration orchestration, only where useful
  Inventory/          Approved balance/movement/reservation use cases
  Suppliers/          Approved supplier commands and history queries
  Purchasing/         PO commands, receipt posting and corrections
  Sales/              Shared channel sale/handover/return use cases
  Orders/             Online order lifecycle and ownership queries
  Checkout/           Quote/order/reservation orchestration
  Payments/           Initialization/reconciliation application use cases
Domain/               Entities and pure stock/state/cost rules
Infrastructure/       EF configurations, external adapters, Identity, workers
```

Controller responsibilities: route/binding, HTTP authorization, antiforgery, cancellation, ModelState/result translation, redirect/view selection. Application responsibilities: authoritative validation, eligibility, permitted transitions, actor/audit context, transaction orchestration and focused DTO queries. Domain helpers express pure rules; Infrastructure implements persistence and external effects. Direct EF in these application services is an intentional practical dependency, not strict framework-independent Clean Architecture.

Recommended concrete additions for current catalogue maintenance: Category listing query/command validation; Product form/snapshot queries; a shared CatalogueValidator; typed catalogue operation outcomes. ProductEditing is already implemented and should be retained, not duplicated. Stock-mapping revision, image storage and unit administration are separate capabilities requiring their own approved rules; their absence is not a reason to allow ordinary edits to bypass restrictions.

Interfaces are genuinely useful for a Paystack/provider client, durable image store/decoded-content boundary, approved email sender, and future POS transport adapter. Put provider-neutral contracts near the consuming application module and implementations in Infrastructure. A typed HttpClient/HttpMessageHandler seam can test provider behaviour without a custom interface for every HTTP wrapper. Use .NET TimeProvider when reservation expiry needs controlled time; no custom clock interface is necessary by default. A trusted current-actor/access abstraction is useful once multiple request/job/API entry points must record authorization and audit identity.

Do not add IProductCreation/ICategoryManagement or a generic IRepository merely to mock EF. SQL-backed tests are important for collation, unique indexes, decimal precision, rowversions, locking and rollback. Use pure-rule unit tests for StockConsumptionRules and fake external adapters for provider failures. Interfaces for internal services should follow a real replacement/consumer boundary, not a blanket naming convention.

## 7. Missing future use cases and dependencies

These are recommendations, not implemented services or authorization to build excluded modules. Inventory, Suppliers, Purchasing and manual shop sales remain owner-requested additions pending commercial approval/business-rule sign-off. POS cashier UI/hardware is future scope.

| Module | Missing service responsibilities | Prerequisites / dependency boundaries |
| --- | --- | --- |
| Product editing follow-up | Focused edit/form query, shared validation and consistent operation result; reviewed identity/stock-rule revision service only if approved. | Editing itself exists. Preserve historical identity, rowversions and current read-only mappings. |
| Catalogue resolution | SKU/barcode lookup and explicit selling-to-base quantity resolution/snapshot. | Existing variants/mappings/units; fail closed on invalid mapping/precision. Reuse across Online, WalkInManual and future POS. |
| Inventory | Balance/availability queries; authorized opening/adjustment/damage commands; immutable movements; reservation create/commit/release/expiry. | Approved location/base-unit rules; physical/unavailable/reserved invariant; deterministic locks/guarded updates; transactional operation keys and actor/reason audit. No balance entities or posting service exists today. |
| Suppliers | Create/edit/deactivate, supplier-product links and purchasing-history queries. | Approved supplier fields/permissions/retention; no supplier entity or CRUD service currently exists. |
| Purchasing | PO draft/issue/cancel/close; receipt/partial-receipt posting; supplier returns/corrections and history. | Supplier/catalogue resolution, approved receipt tolerances and costs, inventory posting. PO creation must not increase stock; accepted receipt alone posts stock atomically. |
| Valuation | Weighted-average cost posting/reconciliation and valuation queries. | Owner requested weighted-average direction; opening/landed cost, tax/freight, return/reversal and unknown-cost policies still require approval. No real valuation report service exists. |
| Sales / Orders | Shared channel sales/handover/returns; online order snapshots, ownership queries and fulfilment transitions. | Online versus WalkInManual/future POS channel identity; preserve online allocations; immutable price/unit snapshots and scoped customer/guest access. Physical stock leaves at handover, not merely payment. |
| Checkout | Authoritative quote/totals and order/reservation orchestration with safe retries. | Approved cart, delivery, discount and quantity rules plus catalogue/stock services. No checkout/cart service exists. |
| Payments | Provider initialization, signed event ingestion, server verification and one reconciliation path for webhook/return/manual reconciliation. | Expected reference/amount/currency/ownership, durable idempotency/inbox/outbox and approved order/reservation transitions. Never hold DB locks during provider network calls or trust a browser return as payment proof. |
| Notifications/reporting | Approved email dispatch/retry and live scoped reporting queries. | Post-commit durable events and permission/privacy rules. Current sample dashboard is not reporting infrastructure. Automated WhatsApp alerts remain excluded. |

Before payment/inventory implementation, define successful-payment allocation, failed/expired release, late-success races, cancellations and actual inspected returns. Provider deduplication and business-operation deduplication must coexist so duplicate callbacks cannot deduct twice. Refund alone must not restock. Use one central stock pool and explicit variant mappings across channels, without automatic unpacking or a separate POS ledger.

## 8. Prioritized recommendations and verification plan

1. **P1 — Close category command validation gaps.** Move normalization, format/length and uniqueness handling to the authoritative use case; remove public IQueryable leakage through a focused listing query. Keep binding and confirmation UI in MVC. Test direct service callers as well as HTTP.
2. **P1 — Establish allowed write paths before additional writers.** Document which bulk operations bypass SaveChanges, require validated commands for conversions/mappings, and introduce explicit caller permissions/actor audit at future import/POS/worker boundaries. Do not add a universal repository wrapper.
3. **P1 — Define concurrency/error contracts.** Distinguish validation, stale version, not-found and unavailable/unknown-commit outcomes. Plan category versioning as a separately reviewed schema change; retain current product/variant rowversions. Narrow SQL exception classification and test wrapped deadlocks and failed lookup recovery.
4. **P2 — Complete catalogue service/query boundaries.** Extract Product form/snapshot reads and shared catalogue validation; keep ProductEditing's atomic aggregate command. Move already-applied status decisions into desired-state commands so future consumers reuse them.
5. **P2 — Make authorization extensible without weakening defaults.** Replace growing controller-type special cases with an explicit supported module-policy convention when new modules are approved. Preserve Admin-only fallback and anonymous denial; test each staff policy separately.
6. **P2 — Separate registration orchestration and improve observability.** Keep Identity managers and secure cookies; move Customer-only registration transaction into a concrete use case. Add redacted operation diagnostics and approved persistent admin auditing as required.
7. **Before commerce/inventory — Approve rules, then build dependent vertical slices.** Catalogue resolution and actor/operation contracts → suppliers and PO intent → transactional balances/receipts/adjustments and approved cost rules → reservations/shared sales/handover → orders/checkout/provider reconciliation → notifications/reporting. Purchasing receipt posting cannot precede inventory safety. Keep proposed POS transport/hardware outside current implementation.

Existing test source covers Identity security, catalogue SQL constraints, category HTTP validation/concurrency, product projections/status, creation/stock mappings, editing/rowversions/rollback and unit seeding. These are console smoke projects, separate from the single web project; a normal root build is not a test run. Preserve SQL-backed coverage and add targeted tests for category direct-call validation/ABA policy, lookup snapshots, typed outcomes, mapping/conversion write bypasses and simultaneous future posting operations. Provider tests should use controlled adapter/HTTP responses; expiry tests should control TimeProvider.

During this audit `dotnet build --no-restore` succeeded with **zero warnings and zero errors**. Database-writing smoke suites, migrations, seeding and browser checks were not rerun; no runtime or database-state verification is claimed. DI verification was source-based. Tailwind was unchanged, so no styling build was required. Only this review document was added.
