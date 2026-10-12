# MonsterASP.NET Git Deploy readiness

Inspected 12 October 2026. This is deployment preparation, not authorization to
modify a production database or enable gated admin functionality.

## Current application

- ASP.NET Core MVC, `Microsoft.NET.Sdk.Web`, target `net8.0`; EF Core SQL Server
  and Identity packages are 8.0.31. Publish is framework-dependent and requires
  the host's .NET 8 ASP.NET Core runtime. No framework upgrade is included.
- Production uses `/Home/Error`, HSTS, HTTPS redirection, authentication,
  authorization and antiforgery. Identity and antiforgery cookies require HTTPS.
- Configuration contains no committed database connection in either appsettings
  file. Development user secrets are not production configuration.
- No migrations or database creation run at application startup. Admin controllers
  remain Development-only and return 404 in Production.
- Static files use `UseStaticFiles`; the layout serves generated `app.css` and
  `site.js` with cache busting. Inter is loaded from Google Fonts.
- Publish generates an IIS `web.config` using AspNetCoreModuleV2, `dotnet`,
  the application DLL and in-process hosting, with stdout logging disabled.

## Blockers and unresolved host prerequisites

1. **SQL Server:** provision a hosted database and configure its connection privately.
   Validate connectivity, TLS and schema before opening account routes. Account
   controller construction resolves the context, so even login GET needs a configured
   connection. Existing migrations cover Identity, catalogue and stock mappings;
   review all three before any approved production schema deployment.
2. **Initial reference data and roles:** public registration requires the Customer
   role. Existing role/admin and unit setup commands and the EF design-time factory
   explicitly reject non-local or non-Development targets. Do not bypass these
   guards, run production as Development, or point local commands at production.
   Agree an authorized production provisioning procedure before launching accounts.
   First-admin access is not enabled by deploying the development admin prototype.
3. **CSS build worker:** `dotnet publish` runs `npm ci` and `npm run css:prod`.
   Confirm Node/npm, dev dependency installation and npm registry access on the
   MonsterASP.NET .NET build worker. Its documented .NET deploy flow does not
   explicitly guarantee these capabilities. If unavailable, resolve the build
   workflow before deploying. `SkipCssBuild=true` is only valid after separately
   building production CSS; do not silently skip compilation on the host.
4. **Data Protection:** confirm the IIS application pool persists keys, protects
   them at rest, and retains them across recycling/deployment. No custom key store
   is configured. Do not store keys in the public static folder or Git. Verify a
   login session survives an application recycle; host configuration may suffice.
5. **HTTPS:** activate a valid certificate before testing accounts. Confirm HTTP
   redirects and HTTPS request handling; do not weaken Secure cookie settings.
6. **Support lifetime:** .NET 8 support ends November 10, 2026. Plan a separately
   tested upgrade before that date; this audit preserves the current target.

Host credentials, repository branch/access, actual database availability, IIS key
storage and remote build capabilities were not verified through a hosting account.
The current storefront is a prototype; deployment does not implement checkout,
payments or commercially gated inventory/purchasing features.

## Control Panel setup

1. Under Websites → your website → Scripting, choose modern ASP.NET Core/.NET,
   with the .NET 8 runtime available and in-process hosting. Keep process bitness
   consistent with any future platform-specific publishing choices.
2. Activate HTTPS. In Scripting → Environment variables set:

   | Name | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `DOTNET_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | Hosted SQL Server connection, entered privately |
   | `AllowedHosts` | Actual site hostname(s), separated by semicolons |

   Use the host-provided SQL server/database/login, SQL authentication, encryption
   and a trusted certificate. Do not reuse local Windows authentication or local
   certificate bypass settings. Do not paste the connection into chat, Git,
   appsettings, a publish profile or deployment logs. Restart after configuration.
3. Resolve the database provisioning and key persistence prerequisites above.
4. Open Git deploy, enter the GitHub HTTPS clone URL ending in `.git`, and select
   the intended branch. For a private repository enter the access token only in
   the host's private field. Choose **Build .NET app** and explicitly select
   `ILLiasu Local Mall.csproj`, since the repository also contains test projects.
5. Enable Git deploy, then Pull latest and Build & publish. Review build logs for
   both the Tailwind compilation and .NET publish. Enabling alone does not publish.
   Manual deployment works on Free; deploy-on-push requires Premium.
6. Confirm the site root contains publish output and generated `web.config`, with
   static assets in its `wwwroot` child directory. Do not deploy source as Static.
   Environment variables are preferred over server-edited secret config files.
   If using a host-only config file, understand Protected files: first deployment
   still copies a repository file; protection prevents subsequent overwrites.

## Local verification

```powershell
npm.cmd ci
dotnet build "ILLiasu Local Mall.csproj" -c Release
dotnet publish "ILLiasu Local Mall.csproj" -c Release -o obj/deployment-check/publish-final
```

Publish itself restores npm dependencies and runs the minified production CSS build.
Node is a build dependency, not an application runtime requirement. Generated CSS
must not be edited by hand. Use a fresh output directory for packaging inspection;
publishing over old output may leave obsolete files.

Verified locally:

- Release build and final full publish succeeded with zero .NET warnings/errors.
  npm installed 27 packages and reported no vulnerabilities.
- Final output contains application assemblies, net8 runtime configuration, generated
  IIS config, public CSS/JS/images. Development appsettings, npm manifests, the .NET
  tool manifest, Bootstrap and Tailwind tokens are excluded. Tests, docs, Styles
  and node_modules were absent from the inspected publish output.
- Published Production app: home, CSS, JS and sample image returned HTTP 200;
  `/Admin/Overview` returned 404. A synthetic connection was used; no real database
  was contacted or migrated. Login GET over HTTP returned 500 because the
  antiforgery cookie requires HTTPS. This was not an HTTPS authentication test.
- Sandbox cache/logging access initially prevented verification; permitted runs
  outside the sandbox completed. These are local execution constraints, not proof
  of host defects.

After deploying, verify HTTPS home/assets, login/register, registration role
assignment, login/logout and antiforgery, generic error responses, admin 404,
database connectivity and cookie continuity after recycle. Do not declare accounts
ready based only on the storefront loading. Keep an earlier publish artifact and
database backup available; application rollback does not undo schema changes.

## Sources

- [MonsterASP.NET Git deploy](https://help.monsterasp.net/websites/git-deploy/github-repository)
- [Host environment variables](https://help.monsterasp.net/websites/configuration/environment-variables)
- [Runtime selection](https://help.monsterasp.net/websites/configuration/scripting)
- [Microsoft IIS hosting and Data Protection](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0)
- [.NET 8 support end date](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)
