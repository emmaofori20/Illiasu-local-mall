# Proposed Phase 1 requirements

Status: reconciled with the proposal on 10 October 2026; formal client acceptance pending. Confirmed proposal content is not evidence of signed client approval.

## Source and status

[client-proposal.docx](client-proposal.docx), “CLIENT BUSINESS & SOFTWARE DEVELOPMENT PROPOSAL”, prepared by Emmanuel Ofori for ILLiasu Local Mall, Ayigya Market, dated 09 October 2026, is the authoritative proposed business scope. It states “Proposal for review and acceptance”; §15 has blank signature/date fields. No separate acceptance evidence was supplied.

DOCX body text was successfully extracted through OOXML, including all 14 tables; no tracked insertions/deletions were found. This verifies content extraction, not rendered layout. §1 reports the Shop Website Requirements Form dated 07 October 2026; the original form was not independently supplied. Citations below refer to actual numbered proposal sections.

[AGENTS.md](../AGENTS.md) governs working practices; the [design skill](../.agents/skills/illiasu-design/SKILL.md) governs presentation, not contractual scope. User instructions require .NET 8 MVC, Razor, EF Core, SQL Server, Identity, Inter and Wharf-inspired layout without its colours. [The screenshot](references/wharf-dashboard.png) is not a feature specification.

“Proposed inclusion” means expressly present in the proposal, awaiting acceptance. “Conditional inclusion” also requires a stated external prerequisite. Outstanding details are listed separately.

## Functional scope confirmed in the proposal

| ID | Status | Proposed requirement and acceptance intent | Source |
| --- | --- | --- | --- |
| FR-01 | Proposed inclusion | Responsive homepage, brand header, featured products, promotions and factual shop information. | §3.1 |
| FR-02 | Proposed inclusion | Category/listing browse with thumbnails, price, variants and availability; search by product name/category; usable empty/results states. Pagination is an implementation choice. | §3.1 |
| FR-03 | Proposed inclusion | Product detail with single/pack/carton/weight variants and item quantity; selected unit and price clear. Variable-weight settlement is not implied. | §§1, 3.1 |
| FR-04 | Proposed inclusion | Edit/remove cart items and recalculate totals; server-validate quantities, prices and availability. | §§3.1, 5.1 |
| FR-05 | Proposed inclusion | Guest checkout or optional account registration/login; name, contact and fulfilment details; pickup/delivery, configured zone fees and total preview. | §§1, 2, 3.1 |
| FR-06 | Conditional inclusion | Ghana Mobile Money through Paystack, subject to merchant approval and supported flows; server initialization, signed callbacks, reference verification and idempotent records. Redirect alone cannot mark paid. | §§2, 3.1, 3.3, 5 |
| FR-07 | Proposed inclusion; history decision pending | Order reference/confirmation and secure tracking; optional account order history. Protect guest order details. | §3.1 |
| FR-08 | Proposed inclusion | Secure admin login and permission-aware routes; optional customer accounts. Use Identity under the project stack; resolve staff permissions/recovery. | §§3.1, 3.2, 5 |
| FR-09 | Proposed inclusion | Contact form, linked phone/WhatsApp, business email/social accounts and privacy/terms links. WhatsApp is customer-to-merchant click-to-chat. | §§2, 3.1, 3.3 |
| FR-10 | Proposed inclusion; permission decision pending | Create/edit/archive categories, products, images, prices, promotions and variants; manual availability. §1 leaves product-editing permission open although §3.2 proposes it. | §§1, 2, 3.2 |
| FR-11 | Proposed inclusion | Filter orders by status/date/payment state; processing, ready, dispatched and completed fulfilment states as appropriate; instructions and manual exceptional-order reconciliation. Separate payment states; never bypass verification. | §§3.2, 3.3, 5.1 |
| FR-12 | Proposed inclusion; rates pending | Admin-configured delivery zones/fees and pickup instructions; client approves names/rates before checkout sign-off. | §§2, 3.2, 12, 15 |
| FR-13 | Proposed inclusion; rules pending | Basic discounts/promotions and highlighted offers; server-side eligibility. Complex coupon features are later scope. | §§3.1, 3.2, 5.1, 13 |
| FR-14 | Proposed inclusion | Moderated post-purchase star rating and short text review; no open anonymous reviews. Resolve purchase verification/guest eligibility and moderator permissions. | §§2, 3.2 |
| FR-15 | Proposed inclusion; definitions pending | Dashboard/sales summaries: paid order totals, order counts, average order value, top products; order/sales CSV downloads. | §3.2 |
| FR-16 | Proposed inclusion; data pending | Structured initial import capped at 100 complete client-supplied product entries; further onboarding quoted. Validate required data, variants/images and duplicates; report rejected entries. | §§2, 12, 15 |
| FR-17 | Proposed inclusion | Admin management of highlighted offers, homepage content and contact details. Agree bounded editable fields; no general-purpose CMS implied. | §3.2 |
| FR-18 | Conditional inclusion | Basic transactional order emails with approved mail service/sender credentials and usage policy; subscriptions separate. Agree customer/merchant recipients and events. | §§1, 2, 3.3 |

## Original proposal exclusions

Proposal §§2, 4, 12, 13 and 15 exclude automated stock quantity tracking, stock decrement/reorder calculations, full inventory ledger/purchasing and low-stock alerts. Manual available/unavailable flags are included; cart/order quantities are purchase data, not inventory tracking. Supplier management was excluded by earlier project instructions; the proposal does not explicitly name a supplier module. The owner now requests centralized inventory, suppliers, purchasing and manual walk-in sales as additions, as recorded below; this does not establish commercial approval.

Other §4 exclusions: native Android/iOS apps; POS/accounting integration; GPS courier/rider dispatch; automatic distance tariffs; SMS or automated WhatsApp Business API alerts; advanced analytics; marketplace/multi-vendor features; loyalty points; subscriptions; unlimited content/data entry; paid advertising; photography; copywriting; regulatory registrations; external provider charges; languages beyond English. §2 also excludes live distance calculations/maps. §13 places advanced coupon complexity and additional staff roles in possible later scope.

Automated WhatsApp alerts are separately quoted optional future work, not fixed Phase 1 (§§2, 3.3, 15); click-to-chat is included. Import beyond 100 complete entries requires additional quotation (§2). Other payment methods, automated refunds and unrequested integrations are not assumed. No SPA conversion is planned.

§10 go-to-market advice does not create application modules: acquisition-source tracking is a business recommendation. §§7–9 commercial projections, fees and allowances are not live product data or performance guarantees. Reconfirm provider offers before procurement.

## Quality, payment, privacy and testing

Proposal §§5–5.2 require HTTPS, hashed credentials, roles, CSRF protection, validation, rate limiting of sensitive endpoints, least privilege and secrets outside source code. Validate image types/sizes and optimize images. Use private source control/environment configuration, error logging, daily backups where supported and documented restoration.

Preserve immutable checkout prices/order-line snapshots (§5.1). Recommended safeguards include decimal money, UTC storage with an agreed reporting timezone, per-order ownership checks, secure guest tracking, minimal personal-data collection, redacted logs and agreed retention. Do not expose private orders through sequential identifiers, sample fixtures or unauthorized exports; do not store payment credentials.

Verify signed callbacks and provider references server-side, matching expected amount/currency/reference/order. Duplicate, failed, retried or out-of-order callbacks must not duplicate orders or sales (§3.3). Manual exception reconciliation may record discrepancies but cannot declare an unverified Paystack transaction paid.

Acceptance (§5.2): current Chrome, Edge, Safari and Android mobile browsers; keyboard-accessible browsing/cart/checkout with labels, focus and contrast; no known critical security findings or blocker defects; sandbox verification and an explicitly authorized live payment test. Admin maintains prices/variants/zones without code changes. Test catalogue, checkout/payment, accounts, mobile views, admin roles and order transitions (§5). WCAG 2.2 AA is a design/testing target, not a claimed certification.

Demonstration, documented UAT, corrected/retested material defects, guide, backup/restore instructions and approved credentials are required (§§5.2, 14). §13 proposes a 30-day defect warranty from production handover, not perpetual maintenance.

## Outstanding client decisions

1. Formal Phase 1 acceptance, §2 decisions, product-editing permissions, optional history and final services agreement (§§1, 14, 15). Blank signatures are not approval.
2. Business/billing identity, designated approver, domain spelling, launch date and commercial terms (§15). The 8–10-week estimate depends on advance payment/assets, gateway onboarding and timely reviews (§6).
3. Logo/final colours, shop profile, complete catalogue/images/prices, variant mapping and import-count convention (§§1, 11, 15).
4. Delivery zones/rates, pickup instructions, fulfilment times, pickup/delivery transition branches, cancellation/refund procedures and tax treatment (§§12, 14, 15).
5. Paystack merchant/test access; approved email sender/provider, credentials, recipients/events and usage costs (§§2, 3.3, 11, 15).
6. Basic discount eligibility/dates/stacking, review purchase/guest eligibility, moderation permissions, editable content fields, reporting period/timezone/refund treatment, account recovery, cart persistence/merge, contact-form routing and data retention.
7. Client-owned Windows-compatible hosting/domain with HTTPS/SQL Server, storage limits, backup retention/restore ownership and recurring costs (§§5, 8, 11, 15).

The current owner instruction authorizes this centralized-inventory planning revision; earlier dashboard previews remain presentation only. Commercial approval, backend implementation and revised release acceptance remain outstanding. No migrations, inventory transactions or supplier CRUD are authorized in this task.

## Requested scope additions - commercial approval pending

Source: project-owner request dated 10 October 2026, not client-proposal.docx. Centralized Inventory, Suppliers, Purchasing and Walk-in Sales are outside the original proposed Phase 1 scope (proposal sections 2, 4, 12, 13, 15). Do not relabel them as confirmed proposal inclusions. Obtain a written change order, revised price/schedule and acceptance criteria before backend implementation or including them in the contracted release.

| ID | Requested addition / acceptance intent | Status |
| --- | --- | --- |
| INV-01 | Physical balances for every product variant; minimum threshold; available, reserved and unavailable quantities displayed separately. No negative available stock or overselling. | Requested; commercial approval pending |
| INV-02 | Low-stock and out-of-stock indicators; dashboard stock on hand, affected variants, estimated inventory value and recent movements. Low-stock means positive available quantity below minimum; out-of-stock means available quantity zero, subject to client confirmation. | Requested; definitions pending |
| INV-03 | Append-only auditable receipts, sales, returns, damage and adjustments with variant, signed quantity, time, actor, source reference and reason. Corrections use linked reversals. | Requested; commercial approval pending |
| INV-04 | Reasoned manual adjustments with restricted permissions; transactional and concurrency-safe ledger/balance changes; reservations and releases throughout checkout/payment/cancellation lifecycle. | Requested; lifecycle approval pending |
| SUP-01 | Create/edit/deactivate suppliers, contact details, product associations and purchasing history; preserve historical references on deactivation. No supplier portal implied. | Requested; commercial approval pending |
| PUR-01 | Purchase orders with variant lines, ordered/received quantities, unit costs, statuses and partial deliveries; creation alone never increases stock. | Requested; commercial approval pending |
| PUR-02 | Record valid accepted receipts against purchase orders; atomically post accepted quantities to inventory once; rejected/damaged quantities handled separately. | Requested; receipt rules pending |
| VAL-01 | Estimated stock value and later valuation reports using owner-requested weighted-average costing, subject to approved landed costs, returns treatment and rounding policy. Prototype values are fictional cost-times-on-hand examples, not accounting reports. | Requested addition; accounting policy and commercial approval pending |

Inventory acceptance must include competing last-unit checkouts, duplicate Paystack webhooks/browser reconciliation, reservation expiry versus payment races, partial/duplicate receipts, rollback on failure, partial cancellations/returns and audit reconciliation. Use SQL Server integration tests. Existing payment verification, privacy, authorization, CSRF, secrets, backup and UAT requirements above remain mandatory. Supplier contacts and purchasing costs require role-limited access and agreed retention; never include real contact data in prototypes.

### Additional unresolved business rules

- Commercial change approval, revised release scope/estimate and staff permissions for buying, receiving, adjustments and viewing costs.
- Single location versus multiple stores; sale-unit precision; whether packs/cartons share physical stock and approved conversion rules; opening balances, opening costs and stocktake approval.
- Threshold based on available or physical stock, equality at minimum, alert recipients/channel and treatment of reserved/damaged/quarantined goods. No automated WhatsApp alerts are authorized.
- Reservation duration, retry/extension limits, when sales deduct physical stock, late successful payments after expiry, cancellation before/after dispatch and inspection/approval before return restocking.
- Supplier required fields, duplicate identifiers, product-versus-variant associations, credit/payment terms and retention; no accounts-payable or supplier payment integration is assumed.
- PO approval/numbering/status rules, receipt evidence, rejected goods, over-delivery tolerance, supplier returns, receipt corrections, closing short orders, taxes, currency, freight and discounts.
- Owner now requests weighted-average costing (see [catalogue packaging review](catalogue-packaging-review.md)); landed-cost allocation, unknown opening costs, historical cost reversals, reporting timezone and accounting sign-off remain unresolved. Do not implement valuation reports until resolved.

## Central shop inventory and sales channels - revised owner decision

The owner has decided that inventory covers the entire physical shop, including online orders and physical walk-in sales. This design decision is recorded; commercial approval of the expanded implementation remains pending. It does not amend the original proposal automatically. This revision is documentation only, including proposed admin navigation; no POS, backend modules or migrations are implemented.

| ID | Expanded requirement | Approval status |
| --- | --- | --- |
| CENTRAL-01 | One authoritative product catalogue and inventory ledger across all sales channels; no separate online or walk-in stock copies. Variant-level balances, explicit units and approved packaging conversions. | Business direction decided; commercial approval pending |
| CENTRAL-02 | SalesChannel values Online, WalkInManual and future POS. Channel is immutable on posted sales/movements; fulfilment (pickup/delivery) and payment method are separate dimensions. POS is planned, not enabled. | Requested addition; commercial approval pending |
| WALK-01 | Authorized staff manually record walk-in sale lines, variants, quantities/units, prices/totals, payment method, transaction reference, sale time and recording actor/time. Posting checks stock and deducts once atomically. Drafts do not deduct. | Requested addition; commercial approval pending |
| WALK-02 | Manual payment records are staff-recorded tender evidence, not Paystack verification. Noncash references are required; cash uses an internal transaction reference. No customer account or personal contact data required for a walk-in sale by default. Correction/void/return actions are reasoned and audited. | Recommended rules; client confirmation pending |
| CENTRAL-03 | PhysicalOnHand includes goods still physically held, including reserved and unavailable goods. Reserved includes unpaid active holds and paid allocations until handover. AvailableToSell = PhysicalOnHand - Unavailable - Reserved; all channels share this balance. | Recommended lifecycle; client confirmation pending |
| CENTRAL-04 | Shared movements, reservations, adjustments, supplier POs and accepted receipts; transactional multi-line posting, concurrency safeguards and operation-level idempotency. Manual availability can additionally block online listing/sale; it cannot manufacture stock. | Requested addition; commercial approval pending |
| CENTRAL-05 | Inventory balances by variant/base unit, reservation/ledger reconciliation, movement history by channel, low/out-of-stock indicators and low-stock admin alerts, supplier purchasing history and approved-method valuation. Separate online-only and all-channel sales totals; never count the same sale twice. | Requested addition; metric/alert/valuation rules pending |
| POS-READY-01 | Stable internal variant IDs, unique active SKU, barcode mapping including packaging/UOM, shared application services and future authenticated/versioned integration contracts. No direct POS database writes. | Architecture planning only; future POS separately scoped |

A future POS cashier UI, scanning workflow, printed receipts, receipt printers, cash drawers, terminals/hardware drivers, fiscal integration and offline synchronization are outside current implementation scope. SKU/barcode data preparation does not authorize hardware or a POS integration now. Rider dispatch and automated WhatsApp alerts remain excluded. Supplier payments/accounts payable are not assumed.

### Channel-wide acceptance and operational limits

Test Online versus WalkInManual competing for the last unit, paid allocations protected from walk-in sale, package conversion without double-counted stock, duplicate manual submissions, duplicate future external requests, stocktake conflicts, partial handovers/returns and ledger reconciliation. Preserve existing Paystack signature/server verification and webhook/browser reconciliation idempotency; successful callbacks allocate stock once and cannot cause repeated physical deduction. A refund alone never restores stock.

Manual recording must occur before goods leave the shop to guarantee available-stock accuracy. If goods already left without recording, an authorized late-entry discrepancy process is needed: do not bypass the no-negative invariant or silently release online commitments. Display effective sale time separately from immutable recording time; agree backdating limits. Accurate cross-channel stock depends on staff recording every physical sale and receipt.

### Additional decisions for centralized inventory

Confirm whole-shop operating procedure and opening stocktake, stock location model, measurable units/precision and pack breaking/repacking. Decide when goods are handed over, paid-allocation duration and uncollected orders; approve the revised physical-stock lifecycle. Confirm walk-in tenders, split payments/credit (not assumed), reference uniqueness, price overrides/discount permissions, void/return approval and late-entry handling. Decide SKU/barcode formats and existing labels, low-stock admin alert recurrence/acknowledgement, all-channel report definitions, accounting valuation and future POS vendor/connectivity requirements. Commercial acceptance and re-estimation remain required.

## Catalogue foundation implementation boundary (11 October 2026)

The current owner instruction explicitly authorizes the six catalogue entities, constraints and additive local migration described in [catalogue-foundation.md](catalogue-foundation.md), supporting FR-02/FR-03/FR-10. This does not authorize catalogue CRUD or the pending inventory/supplier/purchasing/commerce modules. Flat categories are retained because no approved hierarchy requirement was found. Product-specific conversion metadata is variant-scoped; different SKUs are never assumed interchangeable. Original proposal acceptance and expanded-module commercial decisions remain separate. Identity and frontend design are preserved.
