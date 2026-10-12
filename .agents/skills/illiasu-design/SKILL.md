---
name: illiasu-design
description: Design and implement consistent, production-quality, responsive storefront and admin interfaces for ILLiasu Local Mall using ASP.NET Core MVC Razor, Tailwind CSS v4, HTML, and JavaScript. Use for any UI page, component, layout, or visual review in this project.
---

# ILLiasu Local Mall — Frontend Design Skill

## Mission
Create a distinctive, cohesive, accessible grocery-commerce experience, not a generic AI-generated template. Follow the approved project proposal and existing brand assets. Do not invent business facts, product prices, delivery rates, or unsupported features.

## Site-wide design direction — Wharf-inspired structure, NOT colours
- Apply the visual discipline visible in the supplied Wharf dashboard reference to **the entire website**: home, listing/search, product detail, cart, checkout, order tracking, customer account, and all admin pages.
- Borrow typographic hierarchy, balanced density, editorial precision, strong alignment, restrained rounded panels, thoughtful negative space, and polished responsive composition. Do NOT borrow Wharf's palette, amber charts, logo, branding, interface copy, or sample business data.
- Primary typeface: **Inter**, with `system-ui, -apple-system, Segoe UI, sans-serif` fallback. Self-host the font only if licensing and project assets permit; otherwise use a reputable font CDN with a reliable fallback. Avoid mixing unrelated font families.
- Apply font consistently to storefront, admin, forms, navigation, carts, checkout, tables, product prices, and order states. Use `font-variant-numeric: tabular-nums` for prices, totals, KPI numbers, tables and timestamps.
- Suggested desktop type scale (adjust responsively): display 44–52px/1.08 at 650–750 weight; H1 30–36px/1.15 at 650–750; H2 22–26px/1.2 at 650–700; H3 17–20px/1.3 at 600–700; body 14–16px/1.5 at 400–500; small 12–13px/1.45 at 400–500; KPI 30–42px/1.1 at 650–750. Tighten heading letter spacing roughly -0.02em to -0.035em; avoid tight tracking on body text.
- Site-wide layout: 8-point spacing rhythm (with 4-point increments for fine detail), consistent left edges, visually aligned controls, 12–20px card radii, modest separators/shadows, intentional information density and prominent primary CTAs. Use responsive grids; no forced desktop-style card grids on phones.
- Storefront: polished typography-led header/search, editorial hero, category browse, clear product cards with variant and prices, composed product detail, concise cart/checkout, excellent mobile scanning and tap targets. Do not copy the admin dashboard layout into customer shopping pages.
- Admin: use Wharf screenshot for sidebar proportions, dashboard hierarchy, compact metric cards, chart placement, section headers, tables and activity feed, but map metrics to ILLiasu scope. Owner-requested inventory/supplier/purchasing previews are permitted as clearly labelled sample additions pending commercial approval; their backend remains gated below.
- **Official provisional colour direction: Fresh Market.** Primary `#176B45`, neutral background `#F8FAF7`, soft green `#EDF5EA`, accent `#FFBD42`, white surface `#FFFFFF`, text `#202B25`. Client approval is pending. Use semantic Tailwind tokens in `wwwroot/css/tokens.css` across storefront, checkout, customer accounts and admin. Keep headers/navigation, category/product cards and default panels white with neutral borders. Reserve soft green for hero and selected supporting sections. Use primary for CTAs, links, navigation/selection; yellow only for genuine approved offers and promotional highlights with dark text, never white text or random decoration. Preserve Inter, layout and functionality. Derived hover/muted/border/status tokens are documented in `docs/design-system.md`; never scatter hex values in Razor or copy Wharf colours. Maintain contrast and visible focus on light and primary surfaces.
- Make design decisions explicit in `docs/design-system.md` and implement them with central CSS variables for typography, spaces, colours, surfaces, borders, radii, shadows and states. Avoid one-off styling and generic Bootstrap-looking pages.

## Typography examples and constraints
- Page title: compact, high contrast, moderately bold; subtitle underneath in muted body text.
- Section heading: precise and short; contextual action aligned to the opposite edge when appropriate.
- Product price: strong numeric emphasis with GH₵; unit/variant details have visibly lower emphasis.
- Form field: label above input, 14px minimum usable font, concise validation, generous vertical hit area.
- Navigation: medium-weight 13–14px labels, restrained active state, clear keyboard focus.
- Tables: 13–14px labels, tabular digits, readable status tags; convert dense tables to mobile-appropriate cards or scrolling regions with explicit affordance.
- Never shrink critical shopping content to mimic a dense dashboard. Readability and checkout completion override aesthetic fidelity.

## Tech boundaries
- ASP.NET Core MVC with Razor views, C#, standard HTML/CSS/JavaScript.
- Entity Framework Core and SQL Server backend; ASP.NET Core Identity for authentication and roles.
- Tailwind CSS v4 is the primary styling framework, built locally with the official CLI. Do not switch to React, Angular or SPA frameworks unless explicitly requested. Do not use Bootstrap styling or Tailwind's browser CDN.
- Use shared Razor layouts, partial views, view components when warranted, and maintainable scoped CSS architecture.

## Tailwind build conventions

- Use CSS-first `Styles/app.css`, explicit `@source` scanning of `Views/**/*.cshtml`, `Areas/**/*.cshtml` and local JavaScript, and the shared tokens in `wwwroot/css/tokens.css`. `@theme static` preserves existing public variables; `@theme inline` maps aliases such as `font-sans`, spacing and container sizing. Keep approved colours configurable centrally.
- Use literal Tailwind utilities in Razor for layout/product patterns and shared partials for repeated markup. `Styles/components.css` uses `@apply` for shared controls, typography and established storefront patterns; `Styles/exceptions.css` retains only native browser/accessibility rules. Legacy site/storefront stylesheets are removed from the build. Containers use explicit width/max-width utilities; there is no reserved Tailwind container exclusion.
- Preserve the established appearance during migration. Custom `compact`, `tablet` and `phone` variants match the existing max-width 1100px, 900px and 767px boundaries exactly. Keep JavaScript hooks and ARIA states intact and compare before/after screenshots at 375px, 768px and 1440px when possible.
- Write full literal class names: `@(condition ? "bg-brand text-on-brand" : "bg-surface text-text")`. Never use fragments such as `bg-@Model.Colour` or `"grid-cols-" + count`. For finite variants, map to complete static class strings in a scanned Razor/JS file.
- Run `npm ci`, then `npm run css:dev` for watch or `npm run css:prod` for minified production output. `npm run css:build` is a one-shot development build. Generated `wwwroot/css/app.css` is the only stylesheet linked by the layout; never hand-edit it.
- Verify CSS compilation and `dotnet build`; verify static delivery and relevant UI behaviour. Production publish runs the CSS build, requiring Node/npm on the build host, not the deployed IIS server.

## Customer journeys
1. Homepage with shop value proposition, search, categories, featured items, offers and trust signals.
2. Category and search results with clear availability and variants.
3. Product detail with units/packs/cartons/weights as applicable.
4. Cart, guest or signed-in checkout, delivery-zone or pickup choice, Mobile Money initiation, verified payment states.
5. Order confirmation, tracking and optional account history.
6. WhatsApp click-to-chat, contact and policy pages.

## Admin journeys
1. Dashboard with paid sales, order counts, average order value, and top products.
2. Category/product/variant/image/price management; manual available/unavailable flags.
3. Orders, filters, fulfilment and payment status handling.
4. Delivery zones and charges, promotions, moderated reviews, sales reporting and CSV export.

## Scope safeguards
- Inventory, Suppliers and Purchasing are owner-requested additions outside the original proposal, pending commercial approval. Documentation and sample dashboard previews are authorized; do not implement their backend until the change order and business rules are approved. Original Phase 1 excludes automated low-stock alerts and a full inventory ledger.
- WhatsApp ordering is click-to-chat; automated WhatsApp notifications are out of scope.
- Delivery fee is configured by admin-defined zones, not GPS distance.
- Paystack server-side verification and idempotent webhook handling are required; never mark an order paid from a browser redirect alone.
- Initial catalogue import capped at 100 complete client-supplied products.
- Do not imply native mobile apps, multi-vendor marketplace or rider dispatch.

## Execution workflow
1. Inspect repository, requirements, logo, existing CSS and Razor conventions.
2. Before coding, write a concise design direction, page anatomy, responsive strategy, reusable components and design tokens.
3. Implement one representative customer page first (homepage), then one representative admin page; obtain design approval before proliferating patterns.
4. Reuse tokens and components; support empty, loading, error, validation, disabled and success states.
5. Check keyboard focus, form labels, contrast, semantic HTML, image alt text, reduced-motion preference, and mobile tap targets.
6. Preview at ~375px, ~768px and ~1440px when tooling permits. Inspect for clipping, horizontal scroll, typography and checkout friction.
7. Report files changed, what was tested, remaining assumptions and open approvals.

## Required design tokens
Define CSS variables for brand colours, background, surface, border, text, muted text, success/warning/error, typography, 4/8-point spacing scale, radius, shadow and motion. Never hard-code arbitrary colours across views.

## Acceptance bar
- Visual consistency across storefront, checkout and admin.
- Strong hierarchy and believable supermarket product presentation.
- Responsive and keyboard-accessible interactions.
- Realistic Ghana-facing checkout labels and GH₵ formatting, without invented operational claims.
- Clear separation of presentation from payment and business logic.
