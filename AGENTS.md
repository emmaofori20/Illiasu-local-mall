# Codex project instructions — ILLiasu Local Mall

Use `.agents/skills/illiasu-design/SKILL.md` for all UI/UX tasks. Treat the signed client requirements and proposal as scope authority; the design skill governs presentation, not contractual scope. Stack: ASP.NET Core MVC, Razor, EF Core, SQL Server, Identity. Build in vertical slices, test each slice, and do not implement out-of-scope functionality without approval.

Primary styling framework: Tailwind CSS v4 using local `tailwindcss` and `@tailwindcss/cli` dev dependencies. Configuration is CSS-first in `Styles/app.css`; tokens live in `wwwroot/css/tokens.css` as `@theme static`, with aliases via `@theme inline`. Use complete literal utility classes in Razor/JavaScript; never assemble class names from fragments. Use utilities in Razor and shared partials for repeated markup. `Styles/components.css` composes repeated patterns with `@apply`; `Styles/exceptions.css` holds only native browser/accessibility exceptions. Legacy site/storefront stylesheets are removed from the build. Do not add Bootstrap styling or a browser Tailwind CDN.

Preserve existing visuals during migrations. Exact `compact`, `tablet` and `phone` variants retain max-width 1100px, 900px and 767px boundaries. Keep JavaScript hooks/ARIA intact; validate before/after screenshots and interactions when preview tooling is available.

Run `npm ci` initially, `npm run css:dev` alongside the MVC dev server, and `npm run css:prod` for production. Run the CSS build and `dotnet build` after styling changes. The layout serves generated `wwwroot/css/app.css` with cache busting; do not edit generated CSS. Publish runs the production CSS build; CI may use `-p:SkipCssBuild=true` only after separately building CSS. Keep Inter and configurable colours pending client approval.

Site-wide visual rule: follow Wharf-inspired typography and layout discipline in **every** customer and admin page. Use Inter; never copy Wharf colours. Brand colours remain subject to client approval. Reference details in `.agents/skills/illiasu-design/SKILL.md`.

Official provisional colour direction: Fresh Market — primary #176B45, background #F8FAF7, accent #FFBD42, surface #FFFFFF, text #202B25. Client approval pending. Use shared semantic Tailwind tokens for all storefront/checkout/account/admin UI; primary for actions, links and selection, soft green #EDF5EA for hero and selected supporting sections, white navigation/cards with neutral borders, accent only for genuine approved offers with dark text. Preserve layout and functionality; keep hex values out of Razor and maintain clear focus/contrast.


Owner-requested Inventory, Suppliers and Purchasing additions (10 October 2026) are outside the original proposal and pending commercial approval. Documentation and sample admin previews are authorized now; backend transactions, migrations and supplier CRUD remain gated on approved scope and business rules. Preserve the development-only admin restriction until Identity authorization is implemented.
