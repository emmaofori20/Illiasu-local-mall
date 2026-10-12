# Proposed implementation plan

Status: corrected for review, 10 October 2026. [The proposal](client-proposal.docx) defines proposed business Phase 1; formal acceptance remains pending. See [requirements](requirements.md) for section-level mapping. These are engineering stages within that release, not additional contractual phases.

## Milestones and dependencies

The first implementation milestone is shared UI foundations and a polished homepage prototype, followed by an admin dashboard prototype. Both use clearly labelled sample fixtures and no database/authentication/payment integration. Configure EF Core, SQL Server and Identity before implementing any data-driven business features. Then build tested vertical slices with persistence, application rules, authorized endpoints and Razor UI.

| Stage | Deliverable | Dependencies | Exit criteria |
| --- | --- | --- | --- |
| 0 — Review/discovery | Record scope acceptance, open decisions, architecture/plan and product template | Client approver; proposal §§2, 15 | Review completed and implementation authorized. This is a review gate, not the first implementation milestone. |
| 1 — Shared UI/homepage prototype | Inter, configurable neutral tokens, storefront shell, reusable controls/cards and polished responsive homepage | Stage 0; design direction | Visible “Prototype — sample data” label; sample products/prices/offers identified; no functioning order/payment action implied; 375/768/1440px and keyboard checks; homepage design review. |
| 2 — Admin dashboard prototype | Admin sidebar/shell, sample paid-sales/order/AOV metrics, orders table and top-products composition | Stage 1; homepage review | Sample metrics/charts/orders labelled; original sales dashboard plus the separately authorized sample inventory/purchasing extension; no rider widgets; actions demo/disabled; no private data; admin design review before pattern proliferation. |
| 3 — Data/security foundations | EF Core/SQL Server setup, initial migration, Identity roles/admin bootstrap and Admin Area protection | Stages 1–2; database/secrets choices | Build/migration verified on test SQL Server; sign-in works; anonymous/customer admin access denied; no source secrets. Required before business CRUD, persisted carts/orders and real metrics. |
| 4 — Catalogue/browse/cart | Categories/products/variants/images/prices CRUD, manual availability, search/detail and cart | Stage 3; approved designs; product data | Authorized updates; name/category search; valid quantities/variants; unavailable items blocked; tampered prices rejected; original proposal uses manual availability; requested stock tracking follows the gated extension below. |
| 5 — Offers/checkout | Basic promotions, zones/pickup, guest/account checkout and immutable order snapshots | Stage 4; discount/zone/pickup/policy decisions | Server totals/eligibility correct; quote changes surfaced; availability rechecked; duplicate submit safe; customer data private. |
| 6 — Payment/conditional email | Paystack initialization, return, signed webhooks/idempotent reconciliation; basic order emails if sender/service provided | Stage 5; test merchant access; mail credentials for email work | Duplicate/out-of-order/forged/mismatch/timeout cases tested; redirect cannot pay order; mail retries cannot duplicate orders/payments. |
| 7 — Order operations/tracking | Filters, processing/ready/dispatched/completed transitions, exceptions, confirmation, secure tracking and optional history | Stage 6; state/refund/history decisions | Payment/fulfilment distinct; ownership and guest-token tests; pickup/delivery branches validated; exceptions audited without verification bypass. |
| 8 — Remaining proposed scope | Post-purchase moderated star/text reviews; real dashboard/reports/CSV; bounded homepage/contact editing; contact form/social/policy/WhatsApp links | Relevant slices; moderation/content/report decisions | Purchase eligibility/moderation enforced; paid sales counted once; CSV authorized/formula-safe; click-to-chat only; production placeholders removed. |
| 9 — Import/UAT/release | Up to 100 complete supplied products, cross-browser/security QA, UAT, deployment/training/handover | Accepted scope complete; data/hosting/storage/merchant readiness | Import reconciled; no blockers/known critical findings; material defects retested; daily backups where supported and restore checked; authorized live gateway test; guides/credentials delivered. |

Import can start after Stage 4 stabilizes the schema. Contact/policies can proceed earlier but must be approved before public checkout. Email configuration can start with Stage 3; dispatch must follow committed events. Stage 2 is visual only; Stage 8 replaces sample metrics with real queries. Contact-form routing/abuse controls require a decision.

Dependency spine: review → homepage prototype → admin dashboard prototype → EF Core/SQL Server/Identity → catalogue/cart → checkout → payment → fulfilment/tracking → reports/UAT/release.

## Prototype and approval boundaries

Use synthetic fixtures visibly labelled as sample data, never private customer records. Sample offers/prices/sales are not merchant facts. Demonstrate menus/component states; mark unimplemented commerce/admin actions demo or disabled. Do not publish the unprotected prototype as a live admin system. Database/security configuration must precede working business operations.

Review homepage then dashboard before proliferating patterns, following the [design skill](../.agents/skills/illiasu-design/SKILL.md). Neutral colours remain configurable pending final brand approval. Inter and Wharf-inspired typography/spacing/layout apply throughout without copying Wharf colours.

The static prototypes do not satisfy proposal §7's commercial working-prototype trigger: accepted storefront, cart and admin ordering demo. Design review is separate from commercial milestone acceptance.

## Proposal schedule and dependencies

§6 estimates 8–10 weeks after prerequisites: discovery/design week 1; catalogue/UI weeks 2–3; checkout/orders weeks 4–5; payments/reporting weeks 6–7; UAT week 8; deployment/buffer weeks 9–10. Initial UI prototypes fit discovery/design; confirm the refined schedule with the client. Materials, advance payment, gateway onboarding and feedback within two working days remain conditions, not new guarantees.

Outstanding inputs: formal acceptance, logo/colours/catalogue, zones/fees/pickup, policies, Paystack merchant approval, email sender/credentials, hosting/storage and retention/backup ownership. See [requirements](requirements.md). Conditional email does not authorize another messaging integration when credentials are absent.

## Verification

- Prototypes: 375/768/1440px, long names, empty/error states, overflow, Inter fallback, contrast, labels, keyboard/focus and reduced motion. Static sample UI does not require business integration tests.
- Business slices: pricing/discount/delivery, quantity validation, authorization/ownership, review eligibility, contact abuse controls and state transitions. Use SQL Server integration tests for relational constraints, concurrency and transactions rather than relying solely on an in-memory provider.
- Payments: sandbox plus duplicate/mismatch/forged/out-of-order/timeout cases; mail retries separate from payment commit. Explicitly authorize a live test after merchant approval (§5.2).
- Release: current Chrome/Edge/Safari/Android mobile browsers, guest/account paths, admin roles/order states, safe CSV, secrets/PII logs, backup/restore and documented UAT with corrected/retested material defects (§§5, 14).

## Exclusions and current task

Original proposal exclusions remain contractual until a written change order approves Centralized Inventory, Suppliers, Purchasing and Walk-in Sales. Their documentation and sample visual extension are authorized now; backend work is gated below. Rider dispatch and automated WhatsApp alerts remain excluded.

Shared UI/homepage and the development-only admin prototype already exist. This revision updates planning and proposed navigation only; no database setup, migrations, inventory transactions or supplier CRUD.

## Requested extension milestones - pending commercial approval

Source: owner request dated 10 October 2026, outside the original proposal. The original 8-10-week estimate does not cover these additions; re-estimate after rules/change-order acceptance. Maintain the original prototype-first milestone order and Stage 3 EF Core/SQL Server/Identity prerequisite.

| Stage | Deliverable | Dependencies / gate |
| --- | --- | --- |
| 2A - Current visual extension | Inventory/Suppliers/Purchases/Walk-in Sales proposed navigation; sample stock indicators, purchasing and movement panels; reusable Tailwind components | Authorized now; no live mutations; desktop/tablet/mobile and keyboard checks; development-only admin gate retained |
| CR - Change review | Written commercial acceptance, revised estimate, stock/unit/reservation/receipt/permissions decisions and valuation choice | Required before extension backend; valuation reports require explicit costing approval |
| 3 - Data/security foundations | EF Core, SQL Server, Identity and protected Admin Area | Original Stage 3 plus approved change request; no unprotected CRUD |
| I1 - Stock foundations | Variant balances, opening movements, immutable ledger, stocktake/adjustments, unavailable goods and authorized threshold queries | Stage 3; catalogue variants and conversion/location/opening-stock rules accepted; transactional SQL Server tests |
| S1 - Suppliers / purchase orders | Supplier lifecycle/contact/product associations and PO draft/issue/cancel/history | Stage 3; catalogue; approved supplier fields/privacy and PO permissions/statuses; creation changes no stock |
| P1 - Goods receipt | Partial accepted/rejected receipts, posting, PO progress and correction/return rules | I1 + S1; duplicate-receipt/rollback/over-delivery tests; balances and ledger reconcile |
| I2 - Checkout reservations | Atomic multi-line holds, release/expiry, cancellation and audited exception states | I1 + original checkout slice; signed-off hold duration/late-success rules; required before live quantity-constrained checkout |
| I3 - Payment and returns integration | Shared idempotent Paystack reconciliation commits paid allocations once; handover deducts stock; approved cancellation/restock rules | I2 + payment slice; concurrent callback/expiry and partial-return tests; no automatic refund integration implied |
| V1 - Valuation and real dashboard | Approved costing ledger, stock value, inventory/PO queries and authorized reports | Costing sign-off + I1/P1/I3; reconciliation and cost-access tests; remove sample fixtures only when real queries pass |

Supplier forms and PO creation may be developed after foundations without posting stock, but receipts require the ledger. Checkout/payment cannot go live with quantity guarantees until I2/I3 pass. Current sample alerts are presentation only, not a scheduler or notification channel.

Additional acceptance: simultaneous last-unit purchases; multi-line rollback; balance/ledger constraints; duplicate receipts and callbacks; webhook/browser/manual convergence; payment success versus expiry races; unavailable goods; audited corrections; partial delivery/short close; cancellation after receipt; refund without return; inspected damaged returns; repeated partial restocks; staff permissions, contact privacy and cost exports. Run against SQL Server, with fault injection for interrupted transactions and reconciliation after retry.

Recommended next task: review the extended dashboard and settle the change-order/business rules. The first backend task after approval is Stage 3 data/security foundations, followed by variant catalogue and I1 ledger design; do not begin valuation reports before the method is approved.

## Revised shop-wide implementation sequence - commercial approval pending

The owner has decided central inventory must cover online and walk-in sales, with POS integration in a later phase. This supersedes the earlier online-only inventory interpretation and payment-time physical deduction. Current deliverable: three planning documents and proposed navigation, not backend or POS implementation. The original commercial schedule must be re-estimated after change-order approval.

1. **Scope/rules review:** signed commercial change, permissions, units/conversions, stocktake/opening costs, handover/paid-allocation rules, manual payment evidence, late-entry handling and reports. Costing approval is required before valuation work, not a reason to invent a method.
2. **Data/security foundations:** approved EF Core/SQL Server/Identity setup and role policies; protect every admin/business endpoint before real data entry. Keep current prototype restricted to Development until authorization is complete.
3. **Central catalogue and identifiers:** review and approve the [one-table stock-mapping proposal](catalogue-packaging-review.md), then authorize additive migration/backfill and staff-friendly form changes. Preserve variant IDs/SKUs/barcodes and independent selling prices; distinguish shared loose-stock options from sealed separate stock before importing opening quantities. Preserve base-unit precision and immutable conversion snapshots; prevent duplicate stock pools.
4. **Inventory core:** opening ledger, physical/unavailable balances, shared reservation projection, audited adjustments/damage, transactional/idempotent posting and reconciliation. Test canonical-unit conversion and last-unit concurrency with SQL Server.
5. **Suppliers and purchasing:** supplier lifecycle/history, PO issue/partial receipt/short close, valid receipts and correction documents; creation never increases stock. Goods receipt depends on inventory core; approve receipt tolerance/cost rules.
6. **Manual walk-in sale slice:** authorized draft/review/post, sale lines and tender/reference snapshots, immediate handover, stock deductions, void/return handling and idempotent retries. No POS UI/hardware or staff sign-in bypass. Approve late-entry discrepancy handling before enabling use.
7. **Online cross-channel integration:** checkout holds, expiry, verified Paystack paid allocations, late payment exceptions, partial dispatch/pickup handovers, cancellations and inspected returns. Test Online versus WalkInManual simultaneously against one balance before quantity-guaranteed launch.
8. **Reporting and admin alerts:** shop-wide balances/movements/reservations, low/out-of-stock indicators and agreed low-stock admin alert delivery/acknowledgement, channel-filtered sales, supplier purchasing history; approved-cost valuation only after method sign-off. Financial totals must not double-count Order plus Sale.
9. **Operational UAT/release:** full-shop stocktake rehearsal, staff recording process, permissions/privacy/cost access, rollback/retry tests, reconciliation, backup/restore and documented acceptance. Every physical sale must be recorded before handover; train staff and define discrepancy escalation.
10. **Future POS discovery/integration:** separately approved vendor and API adapter, authenticated devices/operators, contract tests and external event mapping against shared services. Cashier UI, printers, drawers, hardware and offline operation remain excluded now.

The first UI/homepage and admin prototype milestones remain the established design foundation. Future navigation uses Inventory, Suppliers, Purchases and Walk-in Sales; retain approved Inter, central semantic colours, reusable Tailwind components and accessible responsive tables/forms. No new administration screens are implemented by this documentation revision.

### Added acceptance scenarios

- Concurrent online and walk-in requests for the last canonical unit, including carton-versus-bottle conversion; a multi-line failure changes no balances/documents.
- Paid online goods still physically present remain reserved until handover; unpaid expiry cannot release a paid commitment; partial handover deducts only its quantities.
- Paystack duplicate/out-of-order callbacks, manual duplicate submissions and future integration duplicate keys; reused key with changed payload fails without posting.
- Quarantine/disposal, adjustments conflicting with reservations, rejected/partial/duplicate receipts, partial returns, refund without return and backdated-sale exceptions.
- Stable SKU/barcode resolution and snapshots after catalogue edits; distinct units are never summed as physical totals; reports separate channel/payment/fulfilment dimensions and count each sale once.
- Operator authorization, supplier-contact privacy, reasoned corrections, audit immutability and reconciliation after interrupted SQL transactions.

### Review decisions before backend

Commercial approval/re-estimate; opening stock/costs; single shop location versus bins/multiple stores; base units and pack conversions/sealed packs; reservation TTL and paid allocation policy; dispatch/pickup evidence; cash/noncash references, split tender/credit and price overrides; void/return permissions; late recording process; supplier terms and PO approval/over-receipt rules; costing method/tax/freight; report/alert definitions and future POS vendor/connectivity. The next authorized implementation after sign-off is data/security foundations, then central catalogue and inventory core.
