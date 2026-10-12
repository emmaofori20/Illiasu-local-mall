# MonsterASP.NET Git Deploy readiness

Inspected 12 October 2026. This is deployment preparation, not authorization to
modify a production database or enable gated admin functionality.

## Current application

Production diagnostics and WebFTP verification are documented in
[production-logging.md](production-logging.md).

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
   Use the explicit guarded workflow in [database-provisioning.md](../docs/database-provisioning.md)
   or its reviewed SQL fallback before launching accounts. The hosted TLS trust
   prerequisite remains unresolved; no production migration is automatic.
   First-admin access is not enabled by deploying the development admin prototype.
3. **Precompiled CSS:** compile production CSS locally and commit it with its hash
   manifest and source inputs using the workflow below. The host no longer needs
   Node/npm. Missing or stale assets block publishing, even with `SkipCssBuild=true`.
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
   `Validated precompiled production CSS; no npm step required.` and .NET publish.
   Enabling alone does not publish. No custom `SkipCssBuild` argument is needed.
   Manual deployment works on Free; deploy-on-push requires Premium.
6. Confirm the site root contains publish output and generated `web.config`, with
   static assets in its `wwwroot` child directory. Do not deploy source as Static.
   Environment variables are preferred over server-edited secret config files.
   If using a host-only config file, understand Protected files: first deployment
   still copies a repository file; protection prevents subsequent overwrites.

## Local verification

```powershell
npm.cmd ci
npm.cmd run css:prod
dotnet clean "ILLiasu Local Mall.csproj" -c Release
dotnet build "ILLiasu Local Mall.csproj" -c Release
dotnet publish "ILLiasu Local Mall.csproj" -c Release -o obj/deployment-check/publish-final
```

CSS compilation is a separate local/CI step. `scripts/build-production-css.mjs`
invokes the pinned Tailwind CLI with `--minify` and records SHA-256 hashes of all
Styles CSS, Views/Areas Razor, public JavaScript, tokens, npm manifests, CSS build
tooling and generated CSS. It removes the previous stamp before compiling so a
failed build cannot retain a valid stamp. `build/ProductionCss.targets` validates
the exact file list and hashes through a .NET MSBuild task, before collecting
publish files. Additions/deletions and changed inputs/output are rejected.
Line endings are normalized so Git CRLF/LF conversions do not produce false failures;
timestamps are not used. If Tailwind source locations change, update both input
lists and rebuild. The manifest is build provenance, not a security signature.

Commit `wwwroot/css/app.css`, `build/production-css.sha256`, build scripts/targets,
package/project changes and current inputs together. Do not manually generate the
stamp or edit CSS. Development/watch builds do not stamp production output; rerun
`css:prod` before deployment. Node is needed only for this frontend preparation.
Publish always validates precompiled CSS, including when `SkipCssBuild=true` is
supplied for compatibility. No npm commands remain in .NET publish.

MonsterASP.NET documents that **Build .NET app** runs `dotnet publish`. Therefore
precompiled tracked CSS is compatible with that documented flow without a custom
host flag; validation needs only its .NET SDK. This conclusion is based on the
documented flow and local tests, not a successful remote redeployment yet. Choose
the MVC project explicitly; do not switch the source repository to Static deployment.
Use a fresh output directory for packaging inspection; publishing over old output
may leave obsolete files.

### Failed deployment diagnosis

The former `BuildProductionCss` target had `BeforeTargets="ComputeFilesToPublish"`
and executed `npm ci` and `npm run css:prod` unless `SkipCssBuild=true`. This explains
why C# Release compilation passed but publishing failed at npm. MSB3073 exit code 1
does not establish npm's underlying cause (cache permissions, registry access,
dependency resolution, or another npm error); the host's full npm log is needed for
that diagnosis. No specific npm infrastructure defect has been assumed. The new
workflow removes that host dependency while retaining asset freshness checks.

### Redeploy

1. Run the local commands above after every relevant source change.
2. Commit and push the source, production CSS and manifest together to the selected
   deployment branch. Do not commit node_modules or local publish output.
3. In MonsterASP.NET Git Deploy, keep **Build .NET app** and select
   `ILLiasu Local Mall.csproj`; Pull latest, confirm the intended commit, then
   Build & publish. No npm install should appear in the publish log.
4. Confirm the validation message, successful publish and HTTPS CSS/JS/image
   delivery. Do not protect `wwwroot/css/app.css` against deployment updates.
   If a hash check fails, rebuild and push both artifacts with the current inputs.
5. Keep existing Production environment/database settings. The separate database,
   role provisioning, HTTPS and key persistence prerequisites above still apply.

Verified locally:

- Clean Release build and publish succeeded with zero .NET warnings/errors after
  separately compiling production CSS. npm installed 27 packages and reported no
  vulnerabilities during local frontend preparation, not during publish.
- Publish also passed with Node/npm removed from PATH and `SkipCssBuild=true`.
  Negative tests rejected an invalid manifest (including with that flag), a missing
  manifest and a newly added scanned JavaScript file. Temporary probes were restored.
- All 37 deployable public CSS/JS/image/vendor assets matched source byte-for-byte
  in the final publish. Generated IIS config uses `dotnet` and in-process hosting.
  Build scripts, manifests and source/build directories were absent from publish.
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
