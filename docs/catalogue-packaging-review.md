# Catalogue packaging and shared-stock review

Status: original review dated 11 October 2026. The owner subsequently approved and authorized
the [mapping database extension](variant-stock-mappings.md), now applied locally, and subsequently
the [staff form workflow](product-selling-options.md), now implemented. Inventory and costing
sections below remain proposals. This stock design expands the original proposal;
commercial approval remains pending. Weighted-average costing is now the requested method,
but its accounting inputs and implementation still require approval.

## Actual model and gaps

Reviewed Product, ProductVariant, UnitOfMeasure, ProductUnitConversion, ProductImage,
their Fluent configurations, ProductCreateInput and the current variant partial.

ProductVariant already supplies a distinct SKU, optional unique barcode, independent price,
unit, quantity step, activation, manual availability and rowversion. Identifiers remain unique
even when inactive. ProductUnitConversion directs one source unit to the same variant's
canonical unit; its composite foreign key enforces product/variant/target ownership. It has
no price, barcode or link to another variant. Conversions cannot currently express another
selling SKU consuming this SKU's stock. There are no implemented inventory balances yet.

Creating a bottle and carton today produces independent variants; recording a carton
conversion on the bottle does not make the separately priced carton variant share stock.
The form's “Canonical unit”, “Factor” and “Variant” labels also expose technical concepts
without explaining what a sale will remove. Inferring sharing from product, weight or UOM
would incorrectly merge sealed cartons or different rice bags.

## Smallest recommended schema extension

Keep ProductVariant as the **selling option and SKU**. Add just one catalogue table,
`VariantStockMapping`, with one required mapping per variant after backfill:

| Proposed field | Purpose |
| --- | --- |
| ProductVariantId (PK/FK) | Selling option, with existing unique SKU/barcode and price |
| ProductId | Same-product ownership enforced by composite foreign keys |
| SoldUnitOfMeasureId | Matches the option's existing UnitOfMeasureId through its composite FK |
| StockVariantId | Variant that owns the inventory pool; may be the selling option itself |
| BaseUnitOfMeasureId | Matches the stock owner's UnitOfMeasureId through its existing composite FK |
| UnitsPerSale decimal(18,6) | Base stock quantity consumed by one sold unit; strictly positive |

FK `(ProductVariantId, ProductId, SoldUnitOfMeasureId)` and FK
`(StockVariantId, ProductId, BaseUnitOfMeasureId)` both target the existing variant alternate
key. Use non-cascading deletes. Index StockVariantId for reverse lookup. For a self-owned
mapping enforce `UnitsPerSale = 1` and sold/base units equal by a SQL check constraint.
No Identity change, new price/barcode table, redundant stock-mode flag or balance is needed.

Ownership rule: stock owners map to themselves; a shared option points directly to that
root, never to another shared option. This cross-row root invariant cannot be enforced
by an ordinary CHECK constraint. Validate it under a Serializable catalogue transaction,
restrict writes to application services, and prohibit retargeting referenced mappings.
Any future imports/integration must use that service; a database trigger is a separate
defence option if direct database writers are later authorized. No cycles or chains.

Existing `UnitOfMeasureId` becomes the **Sold As** unit and Price becomes price per sold
unit. Existing data keeps identical meaning because every legacy option is self-owned
with factor one. QuantityStep and AllowFractionalQuantity describe sold quantities.
The stock owner's unit precision independently constrains normalized consumption.

Retain ProductUnitConversion unchanged: `1 alternate input unit = Factor sold units`
of this exact variant. Normalized stock = input quantity × conversion factor × UnitsPerSale.
Never traverse conversions or infer reciprocals. Each factor and final product must be
representable within the base precision and decimal range. The canonical target FK
continues to be the selling variant's unit, not the stock owner's unit.

Separately priced packs must be explicit selling options with their own SKUs and mappings.
Hide generic conversions behind an advanced section for receiving/counting; conversions
alone do not create a purchasable offer or an independent price. Do not automatically
multiply prices when selecting carton input units. Future transaction contracts must
explicitly distinguish quantity entered in an alternate unit from the selected priced SKU.

## Staff-facing form and proposed input model

Preserve the existing two-column page, Inter, semantic Tailwind v4 tokens, restrained
panels and responsive stacking. Change form terminology, not the approved layout.

1. **Product details:** Product Name, Category, Description, activation and existing slug control.
2. **Selling options:** repeatable option panels with Size / Packaging Option,
   Product Code (SKU), optional Barcode, Sold As, Selling Price (GH₵ per selected unit).
3. **Stock behaviour:** explicit choice “Count this option separately” (safe default)
   or “Use stock from another option”. Shared options choose a stock-owning option
   within this product and enter **Units Per Pack**, labelled with the owner's unit.
   Show an always-visible summary: “Selling 1 carton removes 12 bottles from 500 ml
   bottle stock.” A separately counted option says “Selling 1 sealed carton removes
   1 sealed carton. Loose bottle stock is unchanged.”
4. **Availability and images:** retain activation/manual availability and the existing closed
   upload boundary. Advanced controls contain quantity increments, measured-unit precision
   and optional receiving/counting conversions. Separate-owner factor one is server-derived.

Units Per Pack means consumption, not descriptive contents. For separately counted cartons,
show “Contains 12 bottles” only as Size / Packaging Option text; do not bind 12 as a
stock multiplier. When units are measured, use “Stock quantity per sale (kg/litre)” as
the contextual helper rather than suggesting a fractional number of bottles.

Proposed `ProductCreateInput` retains existing product fields. Rename the displayed collection
to SellingOptions (implementation may keep Variants for endpoint compatibility). Each input:
`ClientRowKey`, Name, Sku, Barcode, SoldUnitOfMeasureId, SellingPrice, StockMode
(Own/Shared), StockOwnerRowKey, UnitsPerSale, QuantityStep, AllowFractionalQuantity,
IsActive, IsAvailable, and optional Conversions. Client row keys identify unsaved rows only;
the server generates IDs, verifies unique keys, resolves same-form roots and derives all FKs.
Never bind stock-owner IDs or balance fields blindly. Editing/importing existing references
must verify product ownership and authorization separately.

Adding/removing rows must preserve stable owner references; block removal of an owner until
dependent options are reassigned or removed. Do not silently change sharing when an option
is deleted, reordered or a unit is changed. Preserve values/errors and focus the error summary;
link inline errors to inputs, announce updated stock summaries, and keep native keyboard
controls. Preview summaries are advisory; server validation is authoritative.

## Illustrative examples — not client inventory or prices

| Product / option | Sold As | Pool | Units Per Sale | Price |
| --- | --- | --- | --- | --- |
| Water / 500 ml bottle, WATER-500-S | Individual | Own bottle pool | 1 bottle | Independently entered |
| Water / carton of 12 loose-stock bottles, WATER-500-C12 | Carton | WATER-500-S | 12 bottles | Independently entered |
| Water / sealed carton of 12, WATER-500-SEALED | Carton | Own sealed-carton pool | 1 carton | Independently entered |
| Rice / sealed 5 kg bag, RICE-5KG | Individual bag | Own 5 kg bag pool | 1 bag | Independently entered |
| Rice / sealed 25 kg bag, RICE-25KG | Individual bag | Own 25 kg bag pool | 1 bag | Independently entered |

With 30 loose bottles and 4 separately counted sealed cartons, selling one shared carton
leaves 18 loose bottles and still 4 sealed cartons. Selling one sealed carton leaves the
loose bottles unchanged and 3 sealed cartons. Do not also count the 30 loose bottles as
2.5 additional stocked cartons. Two carton offers need different real barcodes if scanned;
the same barcode cannot identify both modes. Labels or operator selection need a decision.

A 25 kg bag never becomes five 5 kg bags automatically. Optional loose rice sold by kg
would have its own measured-stock owner, fractional precision and independently priced
selling options. Opening a sealed bag/carton requires a later explicit, approved repack
operation with paired movements and cost transfer; that operation is outside this proposal's
implementation. Equal weight alone proves neither product equivalence nor interchangeability.

## Inventory, costing, purchasing and POS implications

- Future StockBalance, movements, reservations and thresholds use **stock-owning variant +
  location**. Selling variants remain on sale lines; shared options have derived availability,
  never duplicate physical balances. Available = physical − unavailable − reserved.
- Normalize and aggregate all sale lines targeting the same owner before checking stock.
  Lock owners/locations in deterministic order, atomically validate available quantities,
  post documents/ledger/balances/idempotency result, and roll back every line on failure.
  Mapping validation must participate in that transaction or use an immutable approved version.
- Snapshot selling SKU/unit/price, owner, base unit, factors, normalized quantity and cost on
  posted lines. Mapping/unit revisions never rewrite history. Freeze mappings once referenced
  by stock, reservations or purchasing/sale documents; new options/revisions need controlled policy.
- Online, WalkInManual and future POS resolve the same SKU/barcode and mappings. Existing
  payment reservation/handover rules remain: verified payment commits allocation; physical
  stock leaves at handover. Duplicate Paystack callbacks never perform another allocation or
  deduction. Future POS calls shared services with authenticated context/idempotency keys.
- Goods receipts specify the actual destination owner. A supplier carton received into loose
  stock uses an explicit approved factor; a sealed carton receipt increases its own carton pool.
  PO creation never increases stock. Partial receipts post accepted goods once; stock and costs
  commit with the receipt. Supplier purchase history retains entered packaging/cost snapshots.
- Requested costing direction: perpetual moving weighted-average **per stock owner/location**.
  On accepted receipt, new average = (existing carrying value + approved receipt landed cost)
  / new physical quantity; sales issue at the current average, independently of selling price.
  Reservations do not recalculate average. Shared carton cost = 12 × bottle average for twelve
  bottles; independent sealed-carton average remains separate. No FIFO layers proposed.
  Prevent negative stock/value, use higher-precision internal cost/value calculations than
  selling price, and reconcile ledger carrying value with an agreed rounding policy.
- Agree tax/freight/discount allocation, opening/unknown costs, currency, zero-balance reset,
  returns at original issue cost, damage/unavailable valuation, supplier reversals and backdated
  entries before cost posting. Reject or queue backdated postings for an approved recalculation
  policy; never silently change issued costs or use selling price as inventory cost.

## Staged migration and acceptance plan — not executed

1. Approve commercial scope, ownership semantics and staff form; profile current variants,
   conversions and units. Back up the guarded local database and record Identity/catalogue counts.
2. Generate a proposed `AddVariantStockMappings` additive migration only after authorization.
   Add table/FKs/checks/index; backfill every existing variant as self-owned, factor one, same
   sold/base UOM. Infer no shared links from names/barcodes/units. No Identity operations.
3. Review generated SQL and EF model on a disposable SQL Server copy. Ensure exactly one
   mapping for every variant. A mapping PK/FK alone does not force the existence of a child row;
   creation/backfill/service validation must enforce completeness and queries must fail closed
   on missing mappings. Deploy mapping-aware writers/readers together under a maintenance gate.
4. Require staff-reviewed shared links; do not reclassify existing data automatically. Preserve
   price, SKU, barcode, image ownership, existing conversions and all Identity data. Existing
   rows need no price multiplication/division. Review all future reads that assume canonical stock.
5. Test independent/shared/sealed examples, case-insensitive identifier uniqueness, cross-product
   ownership rejection, cycle/chain prevention, decimal overflow/precision, missing owners,
   reordered/deleted rows and atomic rollback on SQL Server. Validate UI at 375/768/1440px.
6. Only after inventory scope approval implement balances/ledger, weighted-average cost policy,
   purchasing/receipts, manual sales, then online reservation/payment/handover and later POS.
   Test competing bottle/carton sales, multi-line rollback, receipt retries and duplicate callbacks.

Rollback before use can remove the new table only. After new shared options exist, reverting
to legacy behavior would falsely make them independent stock; use forward repair or restore
an approved backup under a maintenance gate, never a blind Down migration.

## Decisions for review

Approve the one-table mapping design, same-product-only sharing and immutable direct roots;
confirm which real packages are interchangeable loose stock versus sealed separate stock.
Approve receiving/unpacking procedures (none automatic), barcode policy for sealed/shared
offers, measured precision and quantity increments. Confirm moving-average location scope,
landed-cost/rounding/returns policy, zero-price permission and who may alter stock definitions.
Commercial approval and image-storage decisions remain outstanding. No backend inventory,
checkout, reservation, migration or storefront behavior was changed by this review.
