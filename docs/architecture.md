# Application architecture - ILLiasu Local Mall

Consolidated 12 October 2026 against the implemented source, EF migrations and latest
owner-approved catalogue decisions. This document separates current implementation from
future design; it does not approve commercial scope or claim a fresh database verification.
[Requirements](requirements.md) remains the business-scope record. Inventory, Suppliers,
Purchasing and manual walk-in sales are requested additions outside the original proposal,
pending commercial approval. Future POS is not part of the current implementation.

## Architectural decisions

One production ASP.NET Core MVC .NET 8 project, Razor Views, an Admin Area and one SQL
Server database form a folder-based modular monolith. EF Core is used directly in focused
application services; there are no generic repositories, microservices or generic ProductService.
Test projects are separate harnesses, not additional production services.

Request flow: HTTP binding/authorization/antiforgery in controllers -> application use case
and authoritative validation -> domain rules and EF persistence -> explicit view model -> Razor.
Existing controller read/orchestration exceptions are recorded in the DI section below.
Keep local invariants transactional and never hold SQL locks across external network calls.

## Implemented modules and limits

| Module | Actual implementation | Remaining boundary |
| --- | --- | --- |
| Storefront | Responsive Tailwind/Inter homepage and sample merchandising | Not a database-backed shopping, cart or checkout flow |
| Admin shell/Overview | Dedicated layout, responsive navigation, sample dashboard | Sample metrics are not operational reports; Admin routes remain Development-only |
| Identity | MVC registration/login/logout, standard EF Identity stores, secure cookies, roles and policies | Email confirmation/recovery and MFA are not implemented |
| Catalogue schema | Categories, products, selling variants, units, conversions, images and stock mappings | No balances, reservations or inventory ledger |
| Category Management | Database-backed list/search/filter/pagination, modal create/edit and activation | Flat categories; deactivate rather than hard-delete |
| Product Management | Database-backed list/filter/pagination, dedicated create/edit, activation and mapping validation | Existing SKU/unit/stock mappings are immutable in ordinary editing; no stock posting |
| Unit reference data | Explicit idempotent local seeding; active units populate selling-unit fields | No automatic packaging factors |
| Product-image boundary | Existing IProductImageUploadBoundary rejects persistence | Durable uploads and decoded-image validation await approval |

Inventory, Suppliers, Purchasing, Walk-in Sales, Orders, Payments and POS have no implemented
backend modules, tables or operational services. Sample navigation/dashboard panels do not
establish implementation. [Service review](service-architecture-review.md) records the earlier
audit; [Category refactor](category-service-refactor.md) supersedes its category findings.

## Current project structure

```text
Program.cs                         # Composition root, middleware and routes
Controllers/                       # Home and MVC Account endpoints
Areas/Admin/Controllers/           # Overview, Categories, Products
Areas/Admin/Views/                  # Dedicated shell, partials and management pages
Areas/Admin/ViewModels/             # Admin binding/presentation models
Application/Catalog/               # Four focused services, inputs and query/result DTOs
Domain/Entities/                   # Seven catalogue entities
Domain/StockConsumptionRules.cs    # Pure quantity/precision rules
Infrastructure/Identity/           # ApplicationUser, roles, policies and local provisioning
Infrastructure/Persistence/        # DbContext, mapping validator, unit seeding, design-time factory
Infrastructure/Persistence/Configurations/
Infrastructure/Persistence/Migrations/
Infrastructure/Storage/            # Existing fail-closed image-upload boundary
Views/ and ViewModels/             # Customer Razor and input/output models
Models/ErrorViewModel.cs           # Existing MVC error model
Styles/                            # Tailwind CSS-first entry and shared composition
wwwroot/css/tokens.css              # Central semantic theme tokens
wwwroot/css/app.css                 # Generated stylesheet served with cache busting
tests/                             # Existing SQL/HTTP smoke harnesses; excluded from web globs
docs/                              # Decisions and implementation records
```

Future application folders may separate Inventory, Suppliers, Purchasing, Sales, Orders,
Checkout, Payments and Integrations when those slices are authorized. External adapters belong
in Infrastructure. These names are proposed boundaries, not folders/services already implemented.
No packaged Identity Razor Pages UI is in use: account access uses the existing MVC controller/views.

## Persistence and migrations

ApplicationUser inherits IdentityUser. ApplicationDbContext inherits
IdentityDbContext<ApplicationUser>, applies Fluent configurations and uses SQL Server.
EF SQL Server/design-time and Identity EF packages are 8.0.31. Program reads
`ConnectionStrings:DefaultConnection`; development secrets/environment configuration holds
connection details. The approved local target is Emmanuel / IlliasuLocalMallDb with Windows
authentication. The design-time/provisioning guards restrict local operations; there is no
startup migration or database creation. See [database setup](database-setup.md).

| Migration present in source | Additive schema |
| --- | --- |
| 20261011000039_InitialIdentity | Seven standard Identity tables |
| 20261011002028_AddCatalogueFoundation | Categories, Products, ProductVariants, UnitsOfMeasure, ProductUnitConversions, ProductImages |
| 20261011172240_AddVariantStockMappings | VariantStockMappings with keys, constraints and independent-stock backfill |

Implementation records document these migrations applied locally. This consolidation inspects
migration source/model, not live migration history. Identity tables are AspNetUsers, AspNetRoles,
AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens and AspNetRoleClaims;
there are no custom Users/Roles tables. __EFMigrationsHistory is EF metadata, not an eighth
Identity table. Preserve existing schema and data in future additive migrations.

## Identity, authorization and security

AddIdentity<ApplicationUser, IdentityRole> uses the existing context and standard token providers.
Cookies are Secure/HttpOnly with SameSite=Lax, a 30-minute lifetime and sliding expiration;
security stamps are checked every five minutes. Passwords require at least 12 characters and
upper/lower case, digit and symbol. Lockout is five failed attempts for 15 minutes.
Global browser mutation antiforgery, account rate limiting, server-side input validation and
local-return-url checks remain in place. No JWT is used.

Explicit idempotent development commands seed Admin, InventoryManager, SalesStaff and Customer.
Public registration grants Customer only. First-admin provisioning uses private credentials,
a local-development guard and transactional SQL application lock; no hardcoded password.
See [Identity setup](identity-setup.md) for provisioning commands and recorded verification.

The AdminArea convention disallows anonymous exemptions and applies Admin by default;
Categories and Products explicitly allow Admin/InventoryManager. Overview also requires Admin.
SalesStaff has no implemented module permissions. Area routing and controller guards still
restrict Admin pages to Development despite Identity now being implemented. Future staff modules
must deliberately extend policies/convention coverage; merely adding a sidebar link grants nothing.
No production admin enablement is authorized by this document.

## Implemented catalogue and stock ownership

Category -> Product -> ProductVariant is the central catalogue. Categories are flat.
ProductImage references a product and optionally its variant. Product/variant activation and
manual availability are separate from physical stock. Product and ProductVariant use rowversion;
Category uses signed original-state confirmation and conditional updates, not a rowversion column.
CategoryManagement owns normalization, format/length/uniqueness validation and focused async queries.
SQL constraints remain the final integrity protection.

ProductVariant is an independently priced **selling option** with unique SKU, optional unique
barcode, selling unit, quantity step and fractional-quantity rule. SKU uniqueness includes
inactive variants. Money uses decimal(18,2); explicit quantity/conversion precision is
validated against unit rules. ProductUnitConversion holds directed, product/variant-specific
unit conversions; it does not create a second stock pool or infer sharing between SKUs.
See [catalogue foundation](catalogue-foundation.md).

### VariantStockMapping is the approved stock-owner design

Each selling variant has one mapping, keyed by ProductVariantId, containing ProductId,
SoldUnitOfMeasureId, StockVariantId, BaseUnitOfMeasureId and UnitsPerSale decimal(18,6).
Two composite foreign keys target (Id, ProductId, UnitOfMeasureId) on ProductVariant,
enforcing same-product sold/base-unit references with non-cascading relationships.
Indexes cover selling identity and stock-owner lookup. SQL checks require positive consumption
and self-owned factor one with matching sold/base units.

A separately counted option maps to itself with UnitsPerSale=1. A shared option maps directly
to a same-product self-owned root with an explicitly entered consumption factor. Application
validation rejects chains/cycles, invalid precision and ownership revisions. DbContext SaveChanges
also validates tracked mappings transactionally; shared mapping writes require Serializable
isolation. Arbitrary direct SQL/bulk changes are not a supported validation entry point.
The migration backfills every existing variant as self-owned, factor one, without inference.
See [mapping extension](variant-stock-mappings.md) and [selling options](product-selling-options.md).

Example: single bottles own bottle stock; a separately priced carton offer may explicitly
consume 12 bottles. A sealed carton counted separately instead owns its carton stock and consumes
one carton. No automatic unpacking occurs. Rice bags of 5 kg and 25 kg remain independently
stocked unless an explicitly approved mapping describes genuinely interchangeable stock.
Names, unit labels and SKU patterns never determine sharing or carton prices.

ProductCreation and ProductEditing save aggregate changes atomically in serializable transactions.
Editing validates product/variant rowversions, preserves historical variants by deactivation,
and keeps existing SKU, canonical unit and stock-owner/factor mappings read-only. New options use
stable row identifiers and the same mapping rules. No stock balance is created by these workflows.

### Constructor injection and service boundaries — reviewed 12 October 2026

The current registrations and constructors are consistent. No application code or new
interfaces are required by this review. See the [service architecture audit](service-architecture-review.md)
and subsequent [Category service refactor](category-service-refactor.md) for responsibilities
and validation details; the earlier audit's category findings describe the pre-refactor code.

| Implemented dependency | Constructor dependencies | Registered lifetime |
| --- | --- | --- |
| ApplicationDbContext | DbContextOptions<ApplicationDbContext> | Scoped through AddDbContext |
| CategoryManagement | ApplicationDbContext, IDataProtectionProvider | Scoped |
| ProductManagement | ApplicationDbContext, IDataProtectionProvider | Scoped |
| ProductCreation | ApplicationDbContext | Scoped |
| ProductEditing | ApplicationDbContext, ProductCreation | Scoped |
| IProductImageUploadBoundary → DisabledProductImageUploadBoundary | None; stateless, rejects file persistence | Singleton |

Controllers receive required services through constructors, including C# primary
constructors. CategoriesController uses CategoryManagement; ProductsController uses
the three focused product services and the image boundary. ProductsController still
injects the context for form/reference-data reads, and AccountController uses the
framework Identity managers and context for registration orchestration. Those existing
responsibility improvements remain separate recommendations, not DI defects. Framework
logging, environment, Data Protection and Identity dependencies are registered correctly.

DI conventions:

- Keep CategoryManagement, ProductManagement, ProductCreation and ProductEditing as
  focused concrete use cases. Do not combine them into a generic ProductService or
  introduce interfaces solely to mirror each class. Multiple callers alone do not
  require an interface when they can share the same concrete use case.
- Keep EF Core/LINQ, authoritative business validation and transaction orchestration
  in the application layer. Do not add generic repository or unit-of-work wrappers
  around ApplicationDbContext. ProductEditing currently reuses ProductCreation's
  validation within the same scope; if that coupling grows, extract a focused concrete
  catalogue validator rather than invent an interchangeable creation interface.
- Register context-dependent services as scoped in Program.cs. Do not capture them
  in singletons, construct contexts in controllers/services, inject IServiceProvider,
  or resolve request services through HttpContext.RequestServices. Await context
  operations; do not run parallel EF operations on the same scoped context.
- Preserve IProductImageUploadBoundary. Its current singleton implementation has no
  scoped dependencies and does not save images. Reassess its lifetime when an approved
  storage implementation introduces dependencies; never capture a scoped context.
- Explicit scope creation/resolution in the development provisioning and reference-data
  CLI paths is composition-boundary bootstrap work, not an application service locator.
  ApplicationDbContextFactory constructs a context for EF design-time tooling only.
  Test fixtures may construct contexts explicitly. None warrants a runtime refactor.
- Future hosted workers must create and dispose a scope per operation at the worker
  boundary. Pure static rules and stateless MVC convention objects need no DI abstraction.

Planned interfaces are recommendations only; no integrations or modules are implemented
by this review, and commercial approval gates remain in force.

| Future boundary | Concrete reason for an interface or framework seam |
| --- | --- |
| Payment-provider client | Isolate provider verification/transport from application decisions; substitute failures in tests. A typed HttpClient with a test HttpMessageHandler may be sufficient. |
| Durable product-image store | Separate persistence/decoded-content validation from MVC upload handling, allowing local/cloud adapters. The existing upload boundary is validation, not a storage implementation. |
| Approved email sender | Replace delivery providers and test delivery failure without sending messages. |
| Future POS integration adapter | Keep POS transport/vendor protocols separate from shared application use cases; no POS implementation now. |
| Trusted actor/access context | Consider only when request, API and worker consumers need a consistent authenticated audit identity and authorization contract. |

Put new provider-neutral contracts near their consuming application module and adapters
in Infrastructure. Use framework TimeProvider for testable expiry where needed instead
of a custom clock interface by default. Retain SQL-backed tests for EF constraints,
concurrency and transactions; mocking a repository is not a substitute for those checks.

## Planned centralized inventory and sales - not implemented

Selected business direction: one catalogue and stock system covers the whole physical shop,
including Online, WalkInManual and future POS. Channel is distinct from tender and fulfilment.
Commercial approval and business-rule sign-off are required before implementing these additions.

| Proposed record/module | Purpose and boundary |
| --- | --- |
| InventoryLocation / StockBalance | One balance per stock-owning variant/location; physical, unavailable, reserved and rowversion. Location model needs approval. Shared selling options have derived availability, not duplicate balances. |
| InventoryMovement | Append-only auditable physical/unavailable deltas, source, actor, reason, channel, timestamps, operation key and linked reversal |
| StockReservation / lines/events | Unpaid holds and paid allocations, expiry, partial release/handover and audited transitions |
| Sale / SaleLine / SalePayment | Shared channel sale/tender records; snapshot selling option, owner, unit, factors, normalized quantity, price and cost. Online Order links uniquely to its Sale. |
| Order / OrderItem | Customer/guest ownership, contact/address and quote/fulfilment snapshots; guests require scoped high-entropy access, not order-number-only access |
| PaymentAttempt / ProcessedOperation | Verified provider references, allowed state transitions, idempotency payload/result and duplicate-event protection |
| SaleHandover / SaleReturn | Actual goods leaving/returning, per-line limits, inspection and unique posting keys |
| Supplier / SupplierProduct | Contact details, deactivation, product associations and purchasing history derived from documents |
| PurchaseOrder / GoodsReceipt with lines | Ordered versus accepted/rejected quantities, partial deliveries and cost snapshots; PO creation never increases stock |
| IntegrationInbox / Outbox | Durable external-event deduplication and post-commit notifications where required |

Future posting normalizes quantity through approved conversions and UnitsPerSale, aggregates
lines by stock owner/location, and snapshots factors. Independent sealed cartons remain independent
pools. Receipts identify the actual destination owner; receiving supplier cartons into loose stock
requires explicit approved factors. Repacking/unpacking is a separately reviewed operation.
Low-stock thresholds, out-of-stock indicators, movement reports and supplier purchase history
must use this central model. Variable-weight checkout, offline POS synchronization and hardware
integration are not assumed.

### Physical stock and movement lifecycle

Invariant: `AvailableToSell = PhysicalOnHand - Unavailable - Reserved >= 0`, with each component nonnegative. Reserved includes both unpaid holds and paid allocations; it is a subset of sellable physical stock. Goods not yet physically received or already handed over are not PhysicalOnHand. Financial revenue recognition is separate from physical movement.

| Event | Physical / reservation effect and audit |
| --- | --- |
| Opening stocktake | Post authorized opening movement and cost evidence; establish canonical base-unit balances. |
| PO created/issued | No stock change. |
| Accepted goods receipt | Increase physical once; accepted quarantined goods also increase unavailable until released. Rejected goods not retained are excluded; retained rejected goods need an explicit custody rule. |
| Online checkout | Atomically reserve all normalized quantities from shared available stock; unpaid hold expires under approved TTL. |
| Verified online payment | Mark payment verified and convert hold to paid allocation once. Neither physical stock nor reservation total decreases. Paid allocations do not expire as unpaid holds. |
| Failed/expired unpaid payment or unpaid cancellation | Release active hold once; physical stock unchanged. Late success reacquires all quantities or enters paid stock exception. |
| Online dispatch/pickup handover | Reduce physical and the corresponding paid reservation together once; append sale-out movement and handover document. Partial handover leaves the remaining allocation active. |
| Walk-in draft | No stock change; no lasting reservation proposed. |
| Walk-in sale posting and immediate handover | Revalidate all base quantities against available stock, then atomically record sale/tender evidence, handover and physical sale-out movements once. Online reservations cannot be consumed. |
| Paid online cancellation before handover | Release remaining commitment; no physical increase. Record financial refund/cancellation separately. |
| Return after handover | Increase physical only on actual accepted return, into sellable or unavailable disposition; cap cumulative returned/restocked amounts by handed-over quantities. Refund alone makes no movement. |
| Damage / disposal / adjustment | Quarantine moves sellable quantity to unavailable without changing physical; disposal reduces physical/unavailable. Reasoned adjustments use an authorized document and cannot violate commitments/invariants. Conflicts require investigation/reallocation approval, not silent override. |

Every posting transaction atomically persists documents, normalized balances, ledger/reservation events and idempotency results. Deterministic canonical-variant/location lock order handles multiple sale packs sharing stock. SQL constraints, guarded updates and rowversion/locking prevent Online and WalkInManual from selling the same last unit; retry bounded conflicts, roll back all lines on failure. Never check availability outside the transaction and then post blindly.

Duplicate Paystack webhook, browser return or reconciliation routes converge on one provider-reference/transaction and one sale/payment operation. Duplicate success cannot create another paid allocation, Sale or physical movement. Handover uses its own unique operation key and per-line remaining-quantity limits, so separate handovers cannot deduct twice. Duplicate manual posts or future POS event retries return the prior result; the same key with different payload is rejected. External tender-reference uniqueness is scoped by method/provider after approval, not globally across every cash record. Do not treat staff-entered references as proof of verified Paystack payment.

### Shared channel services and future API boundary

Controllers call application services, never write balances directly. Plan `CatalogQueries` (SKU/barcode resolution), `InventoryQueries`, `ReservationService`, `SalesService` (manual posting/shared sale rules), `HandoverService`, `ReturnService`, `PurchaseOrderService`, `GoodsReceiptService` and `PaymentReconciliationService`. These are folder/service boundaries inside one application, not extra microservices.

- Online MVC checkout uses the reservation/payment/order adapters, shared catalogue and Sales services.
- Admin Walk-in Sales calls an authorized manual-sale command using the same price/unit/stock invariants; payment method does not determine sales channel. A Mobile Money walk-in payment remains WalkInManual.
- Future POS adapter will map external sale IDs, packaging/barcodes and authenticated device/operator context into the same commands. Proposed contracts: catalogue/barcode lookup, advisory stock query, sale post, handover/return and result reconciliation. Any future reserve/release endpoint follows the same lifecycle. Availability responses are advisory; authoritative posting always rechecks transactionally.
- Future `/api/v1/...` endpoints require versioned DTOs, scoped credentials/device and operator authorization, idempotency keys, payload validation, rate limits, redacted audit logs and operation status lookup after uncertain timeouts. No direct SQL access or separate POS stock ledger. External IDs map to stable internal IDs; clients cannot assert trusted actor/channel/verified-payment flags.
- Recommend server-connected POS posting initially. Offline synchronization/stock allocation is not designed or promised now; loss of connection must not silently bypass central stock checks. Vendor selection, contracts and connectivity are future decisions.

An online order and its shared Sale must not both contribute to revenue reports. Define channel filters explicitly; inventory includes every posted physical movement regardless of channel. Low-stock alerts and purchasing queries use the same balances/receipts. Valuation remains gated on the already documented method/landed-cost approval.

## Selected costing direction - accounting approval pending

Use **perpetual moving weighted-average per stock-owning variant/location** as the selected
inventory costing direction, not FIFO. It is a design decision, not an implemented cost ledger
or an approved accounting policy. On an accepted receipt, the proposed average is existing
carrying value plus approved receipt landed cost divided by new physical quantity. Sales issue
at the current average independently of selling price; reservations do not change the average.
Shared options consume their owner's cost; independent sealed-carton pools retain separate averages.

Before cost posting or valuation reports, approve:

- Opening balances/cost evidence and treatment of unknown costs (never silently zero).
- Currency, internal cost/value precision, rounding and carrying-value reconciliation.
- Tax, freight, discounts and landed-cost allocation.
- Zero-balance average reset, damage/quarantine/unavailable-stock valuation and disposal.
- Customer returns at historical issue cost, supplier returns and receipt reversals/corrections.
- Backdated entry/recalculation policy; never silently rewrite previously issued costs.
- Cost-of-sales recognition, reporting timezone, accounting sign-off and cost-view permissions.

No inventory valuation reports exist. Dashboard cost figures remain explicitly fictional samples.

## Planned purchasing and commerce boundaries

PO creation/issue records intent only. Accepted goods receipts alone increase physical stock;
quarantine also increases unavailable stock. Partial receipts accumulate and post once. Receipt,
ledger, balance, cost and PO status changes must commit atomically. Approve PO states/numbering,
authorization, over-delivery tolerance, retained rejected goods, closing short orders and supplier
returns before implementation. Corrections use linked documents, never destructive history edits.

### Planned email and contact

Basic order emails require an approved mail service, sender credentials and usage policy (§§2, 3.3). Inject an email adapter; dispatch/retry after committed order/payment events, with deduplication so email failure cannot undo payment or duplicate orders. A durable dispatch record/outbox is a recommended reliability mechanism, not an additional customer module. Agree recipients/events and retention. Contact form, phone/email/social links and click-to-chat are proposed (§3.1); validate/rate-limit submissions and confirm delivery routing. No automated WhatsApp channel is included.

### Planned checkout and payment boundary

1. Revalidate all variants, quantities, availability, promotion eligibility and delivery settings on the server. Recalculate totals and require reconfirmation if a displayed quote changed.
2. Persist an order snapshot and unique payment attempt before requesting provider initialization. Repeated checkout submissions must not create duplicate payable orders; use an idempotency token.
3. Initialize Paystack through an injected provider client. On timeout, reconcile the existing reference rather than blindly issuing another attempt.
4. Browser return and signed webhook route both invoke the same reconciliation service. Validate webhook authenticity from the raw body using provider documentation at implementation time; verify transaction server-side and compare reference, amount, currency and order ownership.
5. Commit an allowed payment transition atomically with a unique processing record. Duplicate or out-of-order callbacks cannot double-count sales or reverse an already paid order.
6. Display pending until verified; provide safe retry/reconciliation for missing callbacks. Fulfilment cannot start on an unverified payment unless a separately approved rule allows it.

External API payloads/signature details are deliberately not fixed here; validate them against official Paystack documentation when implementing the adapter.

## Presentation and verification conventions

Preserve the existing Inter typography, Wharf-inspired hierarchy/spacing and Tailwind CSS v4
CSS-first pipeline. Styles/app.css detects Razor sources; tokens and semantic aliases remain
centralized. White navigation/cards, neutral background and Fresh Market green/yellow tokens
remain subject to client colour approval. No Bootstrap or browser Tailwind CDN. Use complete
literal utilities, shared partials and small progressive JavaScript; do not edit generated CSS.
See [design system](design-system.md). Planned admin navigation includes Inventory, Suppliers,
Purchases and Walk-in Sales; prototype links/sample metrics are not live modules.

Existing smoke harnesses cover Identity, catalogue constraints, stock mappings, category services
and HTTP flows, product queries/create/edit, and unit seeding. SQL tests remain necessary for
collation, precision, constraints, transactions and rowversion behaviour. For implementation slices,
run relevant harnesses and dotnet build; styling changes also require Tailwind build and responsive
inspection. This documentation-only consolidation does not rerun database-writing tests or builds.

## Superseded decisions and remaining approvals

The earlier VariantPackaging/VariantBarcode candidate is **superseded**, not an additional planned
schema: selling identity/barcode stay on ProductVariant and ownership uses VariantStockMapping.
The older suggestion that sealed cartons require that replacement model is withdrawn; independent
self-owned carton variants cover separately counted stock. Do not implement the obsolete tables.
The initial no-EF/no-Identity/no-CRUD inspection and proposed packaged Identity UI/future-test-folder
statements are removed. Payment-time physical deduction is superseded by the proposed allocation
then handover lifecycle above. The undecided/FIFO-candidate costing statement is superseded by
weighted-average direction. Historical slice documents describe their own dates, not current gaps.

Still approve expanded commercial scope, location/stocktake rules, reservation TTL and late-payment
exceptions, handover/uncollected-order policy, walk-in tenders/reference uniqueness and overrides,
returns/repacking permissions, purchasing workflow and accounting rules. Orders/Payments require
approved state transitions, Paystack merchant setup and provider verification. Future POS vendor,
versioned authenticated APIs, connectivity and hardware remain a later phase. No database/backend
or production release authorization follows from this consolidation.
