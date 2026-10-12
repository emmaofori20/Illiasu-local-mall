# Administration dashboard prototype

Implemented for design review on 10 October 2026. Open `/Admin` with `ASPNETCORE_ENVIRONMENT=Development` (the existing `http` launch profile). This is **not an authenticated or production administration system**.

## Composition and boundaries

Wharf informs the 248px sidebar, compact hierarchy, dominant sales chart beside a two-by-two metric group, supporting panels and recent orders table. ILLiasu branding, Inter and existing semantic green/white tokens remain unchanged. Scoped `Styles/admin.css` uses Tailwind v4 composition; `Styles/app.css` already scans Admin Razor views. Storefront views/styles/JavaScript are unchanged.

Overview is the only implemented page. Orders, Products, Categories, Delivery Zones, Offers & Discounts, Reviews, Reports, Customers and Settings are labelled preview buttons. These and export/order actions open an explicit placeholder notice; they do not load/change records or download exports. No database, Identity, payment, inventory transactions, supplier CRUD, rider dispatch or subscription features were introduced. Stock/Suppliers/Purchases are now sample previews under the requested extension below.

## Sample fixtures

`Areas/Admin/ViewModels/DashboardPrototype.cs` supplies deterministic fixtures, not domain entities. Period links choose 7, 30 or 90 days ending 10 October 2026. Invalid periods fall back to 30 days. Chart points sum to displayed sample paid revenue. Average order value is revenue divided by sample paid-order count; the total order metric includes all sample payment states. Items sold means purchased units, not stock levels. Metric sparklines are decorative samples, not comparative analytics.

Pipeline counts and 64% delivery/36% pickup are illustrative period summaries. They are not reconciled to real fulfilment records or configured delivery zones. Recent orders are a fixed five-row demonstration list; customer labels are fictional and have no contact/address data. Payment labels are sample states only, never evidence of verification. Best-seller photos reuse the licensed temporary assets documented in [sample-image-assets.md](sample-image-assets.md).

## Responsive and accessible behaviour

- Desktop: fixed sidebar, large chart with four side metrics, three supporting panels and full-width orders table.
- At 1100px and below: native modal navigation drawer; Escape, close button and outside click close it, with focus restoration. Native dialog provides modal focus containment. Moving back to desktop closes the drawer.
- Tablet: metrics above the chart, two supporting columns, full-width best sellers. Phone: two-column metrics and stacked panels.
- Tables scroll within labelled, keyboard-focusable regions rather than overflowing the page. The sales chart has an SVG description and expandable daily data table.
- Text accompanies status colours; semantic status tokens, 44px key controls and existing focus/reduced-motion rules remain in use. No authentication/user identity is implied by the workspace shell.

## Production restriction

`Program.cs` registers the Admin Area route only in Development. `OverviewController.OnActionExecuting` separately returns 404 outside Development, protecting against alternate routing. It sends no-store responses. The layout requests no indexing. Environment restriction is a prototype gate, **not authorization**: do not deploy a public server using Development. Before enabling administration, implement Identity roles/policies, authorize all Admin endpoints, replace sample fixtures, apply ownership/CSRF/privacy safeguards and perform security/UAT checks.

## Verification

Tailwind production compilation and .NET 8 build passed with zero .NET warnings/errors. Edge screenshots were inspected at 375, 768 and 1440px: no horizontal page overflow, clipped cards or missing images. Tables retain independent horizontal scrolling. Checked chart period links and 7/30/90 daily-row counts, invalid period fallback, menu opening/focus, Escape closure/focus restoration and placeholder actions. Production requests to `/Admin`, `/Admin/Overview`, `/Admin/Overview/Index` and `/Overview/Index` returned 404; the Development storefront returns 200.

Previews: [desktop](previews/admin-desktop.png), [tablet](previews/admin-tablet.png), [mobile](previews/admin-mobile.png). Full cross-browser/accessibility and authenticated business acceptance remain future work.

## Files

- `Areas/Admin/Controllers/OverviewController.cs`: guarded prototype endpoint.
- `Areas/Admin/ViewModels/DashboardPrototype.cs`: sample dashboard/partial view models.
- `Areas/Admin/Views/Overview/Index.cshtml`: overview composition and chart.
- `Areas/Admin/Views/Shared/`: dedicated layout/navigation, SVG icons, metric cards, panel headings and status badges.
- `Areas/Admin/Views/_ViewImports.cshtml`, `_ViewStart.cshtml`: Area view setup.
- `Styles/admin.css`, `Styles/app.css`, generated `wwwroot/css/app.css`: scoped Tailwind styling and build output.
- `wwwroot/js/admin-prototype.js`: drawer and action-preview interactions.
- `Program.cs`: Development-only area routing.
- Documentation and `docs/previews/`: design/architecture notes and inspected screenshots.

## Owner-requested inventory/purchasing extension

Added Stock, Suppliers and Purchases preview navigation, reusable inventory snapshot/movement row partials and five current-stock metric cards. All quantities, suppliers, costs, PO receipts and movements are fictional. Snapshot metrics do not change with the sales-period selector. The six variant balances total 100 mixed sale units, 13 reserved and 87 available, with two low and two out-of-stock variants; fictional cost-times-on-hand totals GH₵ 3,538. Three sample POs are pending. Selected movements and POs are independent examples, not a full reconciled ledger.

These modules are outside the original proposal and pending commercial approval. No database entities/migrations, transactions, supplier CRUD, auth or payment integration were added. The costing method is undecided. Existing Development-only restrictions remain. Longer navigation scrolls independently; shared semantic status tokens and existing typography/layout are retained.

Extension verification: Tailwind production build and full dotnet build passed (zero .NET warnings/errors). Edge rendering inspected at 375, 768 and 1440px with no page overflow or broken images; movement tables scroll independently. Tested drawer focus/Escape restoration, prototype notices, all three requested sidebar entries, sales-period selection/fallback, stock metric values and five movement rows. Updated desktop/tablet/mobile preview files. Production Admin paths remain 404 and the storefront remains 200.

## Identity protection (11 October 2026)

Admin routes now require the Admin role through server-side MVC authorization, in addition to the Development-only prototype gate. Public customer and staff accounts cannot access the dashboard. Sample navigation/metrics remain prototypes; authentication does not implement management modules. The layout/footer and preview notices now describe authenticated access accurately, with styling preserved. See [identity-setup.md](identity-setup.md) for bootstrap and verification. Earlier verification notes describe the pre-Identity presentation slice.
