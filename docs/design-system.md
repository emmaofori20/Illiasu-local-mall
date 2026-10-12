# Proposed design system

Add Product staff workflow: [product-selling-options.md](product-selling-options.md), based on the
[catalogue packaging review](catalogue-packaging-review.md). The implementation preserves Inter,
the existing layout and semantic Tailwind tokens. Staff labels are
Product Name, Size / Packaging Option, Product Code (SKU), Selling Unit, Selling Price and Units
Per Pack. Explicit separate/shared stock controls and a plain-language consumption summary
replace canonical-unit jargon; advanced receiving conversions remain separate from priced
offers. Native radio controls and polite stock summaries explain separate/shared behaviour;
linked field errors and keyboard focus remain correct after dynamic row removal.

Product Management reuses Category Management table/menu/dialog patterns and the shared
`_AdminPagination`. Thumbnails reserve 48px square space with contain fit and missing-image
fallback. GH₵ prices use tabular numbers; product activation and parent-category warnings
remain distinct. Create uses a dedicated two-column form with reusable variant/conversion partials; Edit now reuses the dedicated form layout with existing options, read-only stock summaries and optional new options; see [product-editing.md](product-editing.md).
No new fonts or colours were introduced.

Category Management now reuses the admin shell and semantic tokens for live records.
Shared `.category-label`, `.category-input` and `.category-errors` patterns compose
Tailwind utilities in `Styles/components.css`. Forms stack below the existing tablet
boundary; labelled category tables scroll within their panels on mobile. Live pages
must not carry the dashboard's sample-data badge. See [category-management.md](category-management.md).

Simple admin forms use the shared `_AdminFormDialog` shell: a white, bordered panel,
semantic backdrop, 24px desktop/20px phone padding and a compact title/close control.
The dialog is capped at 576px wide and the viewport minus 32px; long content scrolls
inside it. Native modal semantics plus explicit Tab wrapping, Escape dismissal and
opener focus restoration support keyboard use. Load and submit states, inline server
errors and focused success feedback accompany progressive MVC enhancement.

Category row actions use a labelled 44px three-dot control and a white, bordered,
192px top-layer popover. Actions use 44px targets and the shared focus/hover tokens.
Pagination uses 44px numbered controls, a green current-page state, disabled boundary
arrows, a labelled page-size selector and a muted result-range summary. These controls
wrap on mobile without adding page-level overflow.

Status: design direction for review, 10 October 2026. Applies to every approved storefront and admin page. Brand colours and business scope remain subject to confirmation.

Page coverage follows [client-proposal.docx](client-proposal.docx) §§3.1–3.2; final colours await client approval (§§1, 15). The proposal is for acceptance, not proof of signed approval. Inter and Wharf-inspired presentation follow the user instruction/design skill.

## Fresh Market provisional brand system

Fresh Market is the official provisional direction selected for design review. **Formal client approval is pending.** It supersedes the earlier neutral placeholder palette. Tokens apply to every existing shared layout/component and must also govern future checkout, customer account and admin pages; those business pages are not implemented by this colour refinement. Inter, hierarchy, spacing, layout and interactions remain unchanged.

| Semantic tokens | Value | Use |
| --- | --- | --- |
| `primary`, `brand`, `link`, `focus`, `selected` | `#176B45` | Primary CTA, actionable links, navigation highlights, selection and focus |
| `brand-hover`, `link-hover` | `#115235` | Hover emphasis |
| `on-brand`, `on-selected`, `focus-inner` | `#FFFFFF` | Text on primary/selected and contrasting inner focus ring |
| `background` | `#F8FAF7` | Page canvas |
| `surface` | `#FFFFFF` | Product surfaces, controls and dialogs |
| `accent` / `accent-hover` | `#FFBD42` / `#F0AD2D` | Genuine approved offers and promotional highlights only; never primary body text |
| `text`, `on-accent` | `#202B25` | Main text and text on accent |
| `text-muted`, `on-disabled` | `#53634F` | Supporting/disabled text |
| `surface-subtle` | `#F8FAF7` | Neutral supporting control surface |
| `soft-green`, `hero`, `promotion` | `#EDF5EA` | Hero and selected supporting sections only |
| `border` / `control-border` | `#DDE2DC` / `#73816F` | Decorative separators / identifiable control boundaries |
| `disabled` | `#E2E9DF` | Disabled control surface |
| `success` / `success-surface` | `#166534` / `#F0FDF4` | Confirmed success with text/icon |
| `warning` / `warning-surface` | `#854D0E` / `#FFF4DC` | Caution with text/icon |
| `error` / `error-surface` | `#B91C1C` / `#FEF2F2` | Validation/failure with explicit message |
| `info` / `info-surface` | `#1E40AF` / `#EFF6FF` | Informational states |

Names map to `--color-*` in `wwwroot/css/tokens.css`, published through Tailwind `@theme static`: e.g. `bg-brand`, `text-link`, `bg-accent text-on-accent`, `border-control-border`. No hex values belong in Razor. Do not use white text on the accent; amber is a supporting colour, not a substitute for warning semantics. Keep text/icon cues for statuses and active/selected states. Real promotion claims still require client content approval.

Calculated contrast: white/primary 6.51:1; text/background 13.96:1; text/accent 8.80:1; muted/hero 5.77:1; primary/background 6.20:1; control-border/white 4.12:1. Essential controls use the stronger border; decorative separators need not serve as control boundaries. The green outer focus outline and white inner ring stay discernible against light surfaces and green controls. These pair checks support accessible contrast but do not constitute a complete accessibility audit.

Fresh Market verification: the production Tailwind build and .NET 8 build pass with zero .NET warnings/errors. Rendered Edge previews at 375px, 768px and 1440px show no horizontal overflow; sampled geometry and typography are unchanged from the previous palette. Mobile navigation, sample search, category filtering, empty results and preview dialogs still work. Desktop and mobile screenshots were visually inspected. This does not replace a full accessibility or cross-browser audit.

Colour balance refinement: white header/navigation, category and full product card surfaces, and footer sit against the neutral page canvas. Soft green is reserved for hero and selected supporting panels. The decorative pantry badge is white; no current sample offer warrants yellow. Future checkout/account/admin layouts must follow the same semantic surface rules. All layout, type and responsive rules remain unchanged.

## Tailwind implementation

Tailwind CSS v4 is the primary framework for future UI work, using the official local Node/npm CLI, not a browser CDN or Bootstrap. `package.json` pins `tailwindcss` and `@tailwindcss/cli`; `package-lock.json` locks their dependency tree. Install with `npm ci`. No homepage implementation or redesign is part of this infrastructure change; the existing prototype remains.

Source: `Styles/app.css`. It imports Tailwind, the token file, utility-composed `Styles/components.css` and minimal `Styles/exceptions.css`. Explicit `@source` globs detect `Views/**/*.cshtml`, future `Areas/**/*.cshtml` and `wwwroot/js/**/*.js`, relative to the stylesheet, with automatic scanning disabled to avoid build artifacts/vendor code. No v3 `content` array or `tailwind.config.js` is needed. Containers use literal width/max-width utilities; the former container exclusion is removed.

`wwwroot/css/tokens.css` uses `@theme static` to preserve existing variables and expose their matching colour, font, radius and shadow utilities. `Styles/app.css` maps existing spacing variables and the Inter font with `@theme inline`, avoiding duplicate values. Examples: `bg-brand`, `text-text`, `text-text-muted`, `border-border`, `rounded-card`, `shadow-card`, `font-sans`, `p-6`, `gap-4`, `max-w-storefront`, `duration-fast` and `ease-standard`. Colours remain temporary and centrally configurable. Inter retains the existing Google Fonts load and system fallback.

Commands from the project root:

```powershell
npm ci
npm run css:build  # one-shot, readable development CSS
npm run css:dev    # watch in one terminal; dotnet watch in another
npm run css:prod   # minified production CSS
dotnet build "ILLiasu Local Mall.sln"
dotnet publish "ILLiasu Local Mall.csproj" -c Release
```

Output is `wwwroot/css/app.css`, served through existing `UseStaticFiles()` and linked with `asp-append-version="true"`. Do not edit output manually. Run `npm ci` and `npm run css:prod` before publishing; the production script compiles minified CSS and records input/output hashes in `build/production-css.sha256`. Commit both generated files alongside source changes. Publish validates the hashes with .NET only and never invokes npm. `SkipCssBuild=true` does not bypass this validation. Rebuild whenever tokens, source CSS, Razor, scanned JavaScript, package files or build tooling changes. Development/watch output cannot replace stamped production output. See [deployment workflow](monsterasp-deployment.md).

Use complete literal utilities in Razor/JS. A conditional may select complete strings, e.g. `@(active ? "bg-brand text-on-brand" : "bg-surface text-text")`; never construct fragments like `bg-@Model.Colour` or `"grid-cols-" + count`. Finite class maps must live in scanned Razor/JS sources. Avoid broad safelists.

Bootstrap is not loaded by the storefront; unused vendor assets are excluded from publish rather than deleting unrelated libraries. jQuery validation assets remain for the existing validation partial. Styling uses literal utilities/shared Razor partials and `@apply` composition; the unused `site.css` and `storefront.css` are deleted. Verify modern browser support against Tailwind v4 requirements before defining production support commitments.

### Preservation migration

Reusable product cards, price/variant presentation, prototype shopping buttons, product/category grids and container sizing now use complete literal Tailwind classes in Razor. Existing class hooks, data attributes, JavaScript interactions and content are retained. Shared header/footer, hero, section headings and controls are expressed with Tailwind `@apply` in the components layer rather than parallel legacy CSS. Exact existing typography/composition values use arbitrary-value utilities where rounding to a stock Tailwind scale would change the design. Repeated product markup stays in `_ProductCard.cshtml`.

Custom `compact`, `tablet` and `phone` variants preserve max-width 1100px, 900px and 767px. `Styles/exceptions.css` retains only tap-highlight behaviour, the precise combined link/button transition, native `[hidden]` handling, screen-reader clipping and reduced-motion overrides. It is a small intentional exception stylesheet, not remaining Bootstrap styling. Existing raw selector names in components are reusable/behaviour hooks, not a second styling framework. Do not build class fragments dynamically in Razor.

Migration verification: final headless Edge captures are byte-for-byte identical to the pre-migration baseline at 375px, 768px and 1440px. Sampled DOM bounds and computed typography, colour, padding, gap and display values also match, with no horizontal overflow. Inter loads; mobile navigation, sample search, category filtering, empty results and preview dialogs still work. Development/minified CSS builds and the .NET 8 build pass (zero .NET warnings/errors); generated CSS returns HTTP 200. No visual regressions were observed in these checks. This is not a full Safari/Firefox or accessibility audit.

Infrastructure verification: pinned official CLI/framework v4.3.0 with compatible transitive fixes, zero npm audit vulnerabilities at setup; development and minified CSS builds passed. A temporary Razor source probe verified token utility generation, then was removed and production CSS rebuilt. .NET 8 Debug build and Release publish passed; publish included generated CSS and excluded Bootstrap/node_modules. Local HTTP delivery and the versioned stylesheet link were verified. Existing homepage markup/features were not implemented or redesigned in this change.

## Initial prototypes

First implement shared UI foundations and a polished homepage prototype, then an admin dashboard prototype. Both visibly state “Prototype — sample data”. Label sample products/prices/offers and dashboard metrics/charts/orders; use no real customer information or invented operational claims. Menus/component states can be demonstrated; unimplemented commerce/admin actions are demo/disabled. These prototypes need no database/authentication integration, but must not be published as a live admin system. Configure EF Core, SQL Server and Identity before data-driven business features; replace fixtures before production. These static designs do not satisfy the commercial working-prototype milestone (§7).

### Implemented homepage prototype

The homepage now uses an editorial two-column hero with temporary licensed grocery photography, compact photographic category selectors, a responsive sample-product grid, one promotional preview and an open shop-information section. At phone widths the header/search and hero stack, the menu collapses and the products use two columns; tablet uses three and desktop four. The implementation uses shared header/footer/product-card partials and a presentation-only sample record, with no database entities or business services.

`wwwroot/css/tokens.css` is the current implementation authority: Fresh Market provisional primary (`#176B45`), background (`#F8FAF7`), accent (`#FFBD42`), surface (`#FFFFFF`) and text (`#202B25`). These are the official provisional tokens pending client approval, not a formally approved palette. The documented token example below remains a proposed baseline; the implemented homepage adds `--color-hero`, `--color-promotion` and `--container`. Razor utilities and `Styles/components.css` supply composition; `Styles/exceptions.css` supplies browser/accessibility exceptions. Photographic colours belong to temporary stock assets, not brand tokens.

Inter loads through Google Fonts with the specified system fallback; licensed self-hosting remains a later option. Products, GH₵ prices, availability and stock photographs are visibly sample content. Search/category filtering operates only on eight sample fixtures; available products’ Add to Cart buttons and account/cart controls open explicit prototype dialogs; the unavailable sample is disabled. No cart mutations, checkout, authentication, live promotions, policy collection or contact submission exists. Approved logo, client packshots/prices/availability, promotional copy and contact details remain outstanding. See [sample-image-assets.md](sample-image-assets.md) for all photo credits, source links, licensing, delivery rules and verification. The user approved the existing typography, colour balance, layout and spacing for this merchandising refinement; formal contractual/client acceptance is separate.

Verification: solution build passed with zero warnings/errors; headless Edge screenshots inspected at emulated 375px, 768px and 1440px with no horizontal overflow. Inter loading, sample search/category counts, empty-result display, mobile-menu expansion and prototype dialog opening were checked. These checks do not establish a full accessibility or cross-browser audit. The approved storefront remains unchanged; the separate admin prototype is documented below.

### Implemented administration prototype

The development-only `/Admin` Overview now uses a dedicated Admin Area layout, 248px sidebar, large sales chart, four metric cards, fulfilment pipeline, delivery/pickup mix, best sellers and recent orders. All data is explicitly sample content. Inter and shared tokens are preserved; scoped Tailwind composition lives in `Styles/admin.css`. Preview actions have no business side effects. Native modal navigation at 1100px and below supports Escape/focus restoration, and tables scroll within their regions. See [admin-prototype.md](admin-prototype.md) for fixtures, production restriction, changed files and 375/768/1440px verification.

## Reference interpretation

The [Wharf screenshot](references/wharf-dashboard.png) uses a roughly 255px sidebar, clear title/subtitle hierarchy, aligned section edges, a dominant information panel balanced with smaller panels, compact numerals and restrained table separators. Borrow that alignment, hierarchy, whitespace and balanced density. Do not reproduce its cream/amber palette, gradient, logo, unrelated business features, copy or sample metrics.

Storefront pages use editorial shopping composition, product imagery and an obvious shopping action; admin pages use a sidebar and compact operational hierarchy. Both share typography, spacing, controls and status patterns. Do not force the dashboard shell onto checkout or shrink shopping text to dashboard density.

## Typography

Primary font: **Inter** everywhere, fallback `system-ui, -apple-system, "Segoe UI", sans-serif`. Prefer licensed self-hosted WOFF2 with `font-display: swap`; retain license notices. No font asset is currently supplied. If self-hosting is unavailable, review a reputable CDN source with a dependable fallback before use.

| Role | Mobile → desktop | Line height | Weight |
| --- | --- | --- | --- |
| Display/hero | 32 → 52px | 1.08 | 700 |
| H1/page title | 28 → 36px | 1.15 | 700 |
| H2/section | 22 → 26px | 1.2 | 650 |
| H3/card title | 18 → 20px | 1.3 | 600 |
| Body and shopping forms | 16px | 1.5 | 400–500 |
| Navigation/admin table | 14px | 1.45 | 500; headers 600 |
| Supporting text | 13px | 1.45 | 400–500 |
| KPI | 30 → 42px | 1.1 | 700 |

Use heading tracking around -0.025em, body tracking normal. Use available font weights rather than relying on synthetic styles. Prices, totals, KPIs, timestamps and tables use `font-variant-numeric: tabular-nums`. Emphasize GH₵ prices; unit/variant labels remain subordinate but readable. Use 16px inputs on mobile and a restrained line length of about 65 characters for policy/help copy.

## Spacing and composition

- 8px rhythm with 4px detail increments: 4, 8, 12, 16, 24, 32, 40, 48, 64px.
- Storefront container: proposed maximum 1280px; horizontal gutters 16px phone, 24px tablet, 32px desktop.
- Section gaps: 32px phone and 48–64px desktop; card padding 16–24px; form groups 16–24px.
- Admin sidebar: proposed 248px on wide layouts, main content padding 24–32px; section title/action alignment mirrors the reference's discipline.
- Card radii 12–20px; controls 8px; subtle borders and one light elevation level. Do not wrap every content block in nested cards.

## Proposed central tokens

The following CSS is an illustrative baseline, not a second token source. The implemented authority is `wwwroot/css/tokens.css`, now expressed with Tailwind v4 `@theme static`, with aliases in `Styles/app.css`. Fresh Market is provisional; any client-approved changes must replace semantic variables centrally. Status colours are functional proposals, not client brand decisions, and must be checked for contrast in actual components.

```css
:root {
  --font-primary: "Inter", system-ui, -apple-system, "Segoe UI", sans-serif;
  --font-body: 1rem;
  --font-small: 0.8125rem;
  --font-nav: 0.875rem;
  --font-display: clamp(2rem, 4vw, 3.25rem);
  --font-h1: clamp(1.75rem, 3vw, 2.25rem);
  --font-h2: clamp(1.375rem, 2vw, 1.625rem);
  --font-h3: clamp(1.125rem, 1.5vw, 1.25rem);
  --font-kpi: clamp(1.875rem, 3vw, 2.625rem);
  --weight-regular: 400;
  --weight-medium: 500;
  --weight-semibold: 600;
  --weight-bold: 700;
  --leading-body: 1.5;
  --leading-heading: 1.15;
  --tracking-heading: -0.025em;
  --space-1: 0.25rem;
  --space-2: 0.5rem;
  --space-3: 0.75rem;
  --space-4: 1rem;
  --space-6: 1.5rem;
  --space-8: 2rem;
  --space-10: 2.5rem;
  --space-12: 3rem;
  --space-16: 4rem;
  --color-brand: #176B45;
  --color-brand-hover: #115235;
  --color-on-brand: #ffffff;
  --color-background: #F8FAF7;
  --color-surface: #ffffff;
  --color-surface-subtle: #F8FAF7;
  --color-border: #DDE2DC;
  --color-control-border: #73816F;
  --color-text: #202B25;
  --color-text-muted: #53634F;
  --color-focus: #176B45;
  --color-success: #166534;
  --color-success-surface: #f0fdf4;
  --color-warning: #854d0e;
  --color-warning-surface: #fff4dc;
  --color-error: #b91c1c;
  --color-error-surface: #fef2f2;
  --color-info: #1e40af;
  --color-info-surface: #eff6ff;
  --radius-control: 0.5rem;
  --radius-card: 1rem;
  --radius-panel: 1.25rem;
  --radius-pill: 999px;
  --shadow-card: 0 4px 16px rgb(0 0 0 / 0.06);
  --motion-fast: 120ms;
  --motion-normal: 180ms;
  --motion-ease: ease-out;
  --container-storefront: 80rem;
  --sidebar-width: 15.5rem;
  --control-height: 2.75rem;
  --focus-width: 3px;
  --focus-offset: 3px;
}
```

Define additional semantic variables only for a concrete reusable need, including hover/disabled states and approved chart series. Product photography supplies visual richness alongside the provisional Fresh Market palette. Keep literal colours out of views. The selected Fresh Market accent is independent of the Wharf reference; borrow only its typography, spacing and layout discipline.

## Reusable components and states

| Component | Responsibilities |
| --- | --- |
| Storefront header/footer | Brand, prominent search, category navigation, account/cart actions; supplied contact/policy links. |
| Admin shell | Role-appropriate navigation, title/subtitle, contextual action; accessible mobile drawer. |
| Page/section header | Consistent title, supporting copy and aligned action; sensible wrapping on mobile. |
| Buttons and links | Primary, secondary, quiet and destructive intent; visible hover/focus, disabled and submitting states. |
| Form field | Persistent label, hint, required indication and linked error; summary on submission; native controls first. |
| Product card/detail summary | Image with reserved ratio, title, variant/unit, GH₵ price, availability and valid action; tolerate long names. |
| Variant selector/quantity control | Clear selection and unit, keyboard access, quantity bounds, unavailable state. |
| Cart line/order summary | Variant and quantity, price breakdown, delivery/discount/total; server-confirmed amounts. |
| Status badge/alert | Text plus optional icon; distinct payment and fulfilment meaning; never colour alone. |
| Metric card/report panel | Label and agreed period; visibly labelled sample values in prototypes, verified computations in production; accessible chart text/table alternative. |
| Data table/list and pagination | Clear headers, filters, action labels, empty state; responsive adaptation with all critical information retained. |
| Review/promotion patterns | Proposed basic offers and post-purchase moderated star/text reviews (§§2, 3.2); no open anonymous reviews; resolve details before business implementation. |
| Contact/content patterns | Accessible contact form and phone/email/social/WhatsApp links; bounded admin homepage/contact editing (§§3.1, 3.2). |

For each relevant component specify default, hover, focus-visible, active, disabled, loading, empty, error/validation and success states. Do not show nonfunctional actions as completed features. Use Razor partials for shared markup and view components only when reusable data logic warrants them.

## Page anatomy

- Home: header/search → approved value proposition/hero → category browse → featured/offer products → factual trust/contact content → footer. No generic mandatory three-card block.
- Listing/search: title/result count → filters/sort only as approved → responsive products → pagination/empty results.
- Detail: images and product information → variant/quantity/action → approved supporting detail. Buying controls are visually dominant.
- Cart/checkout: editable items or fulfilment/payment fields alongside an explicit total summary; mobile summary follows logical form flow. Pending payment is distinct from confirmation.
- Tracking/account: secure order context → payment/fulfilment status → items and approved actions/history.
- Admin: title/period → paid-sales/order-count/AOV metrics → orders/top-products/reports as proposed (§3.2). Fulfilment labels use processing/ready/dispatched/completed as appropriate, distinct from payment status. The original proposal excludes inventory/suppliers/purchasing. The owner-requested extension now adds clearly labelled sample stock, supplier and purchase previews pending commercial approval; no backend workflows yet. Rider dispatch and automated WhatsApp alerts remain excluded.

## Responsive behaviour

Use content-driven breakpoints, initially 640px, 768px and 1024px. These are layout choices rather than device assumptions.

- Phone (~375px): compact header, search on its own row, usable menu, single-column checkout/detail and metric stack. Products use one or two columns only when names, prices and 44px controls fit without truncating critical content.
- Tablet (~768px): flexible 2–3-column product grid; detail can remain stacked; admin drawer persists until sidebar leaves enough readable working width.
- Desktop (~1440px): contained editorial storefront, 3–4-column products as content permits, two-column detail/checkout and persistent admin sidebar; metrics align on a consistent grid.
- Dense tables become labelled cards when useful or use a labelled horizontal scroll region with a visible affordance. Never make the entire viewport scroll horizontally.
- Sticky summaries/actions must not obscure content, keyboard focus or validation. Overlay drawers restore trigger focus and support Escape.

## Accessibility and validation

Use semantic headings/landmarks, a skip link, descriptive accessible names, persistent labels and useful image alt text. Aim for at least 44px shopping touch targets. Verify text contrast (4.5:1 normal, 3:1 large) and essential control/focus contrast (3:1) with final colours. Use visible focus and status text; distinguish disabled from actionable controls without relying only on reduced opacity.

Respect reduced motion; prefer brief colour/opacity transitions and remove nonessential animation when requested. Reserve image dimensions to reduce layout shifts; optimize product imagery without distorting it.

Before design approval, inspect homepage and representative admin page at 375, 768 and 1440px, including keyboard flow, long content, empty/error states, clipping and mobile checkout readability. Then apply approved patterns consistently to every confirmed page. Homepage render verification is recorded above; authenticated admin workflows, checkout and a full accessibility audit remain future work.




## Requested admin extension (10 October 2026)

Preserve the Wharf-inspired sales/chart hierarchy, Inter and existing shared green/white tokens. Add a separate current-stock snapshot, independent of sales-period selectors: mixed variant sale units, available/reserved detail, low/out-of-stock counts, fictional cost-times-on-hand value (method pending), pending POs and recent movements. Use shared metric, panel-heading, status and table partials. White panels, neutral borders and semantic warning/error badges communicate attention with text; yellow is not decorative promotion.

Stock, Suppliers and Purchases navigation opens explicit preview notices. Label all fixtures sample and these modules pending commercial approval. No real contacts or costs are exposed. Dense tables scroll within labelled keyboard-accessible regions; the longer desktop sidebar and mobile drawer remain scrollable. Retain 375/768/1440px verification and the production restriction until Identity authorization exists.

## Account access pages (11 October 2026)

Registration, login, account, logout confirmation and access-denied pages use a dedicated shared account layout, Inter, existing semantic tokens, restrained 480px form width, white bordered panels, 16px inputs and shared green/secondary buttons. No changes to homepage/dashboard styling. The storefront Account control now navigates to real account access. Forms use persistent labels, linked validation, masked password fields, autocomplete hints, antiforgery and a shared password help partial; logout mutation is POST-only. Inspected Login and Register at 375/768/1440px with Inter loaded and no horizontal overflow.
