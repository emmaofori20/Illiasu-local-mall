# Production diagnostics on MonsterASP.NET

Normal web startup never provisions a database. This change does not alter users,
roles, connection settings, authentication rules or the development admin guard.

## Findings

Previously the application used the default Microsoft logging providers without a
file provider. Console messages require IIS stdout capture to become files; the
published IIS web.config disables that capture. The hosting debug switch and its
“No log file yet” indicator describe host stdout capture, not application rolling
logs. Inspect the deployed web.config to determine the actual capture setting.
Identity previously returned a generic failed-login result without an explicit
diagnostic event. An empty user table explains rejected sign-ins, but does not
explain worker terminations. Their cause requires the actual IIS/WAS events and
host resource information; no hosted database was accessed for these checks.

## Logging behavior and privacy

Serilog replaces the default web logging providers and writes newline-delimited
JSON to console and `App_Data/logs/appYYYYMMDD*.jsonl` beneath the application
content root. This directory is outside wwwroot. Optional `Diagnostics:Directory`
must remain inside App_Data and outside the web root; unsafe paths and filesystem
links are rejected. Published output excludes runtime logs.

Information-level application events include startup, graceful stopping/stopped,
managed-memory samples, Identity outcomes and HTTP completion. Microsoft/System
events are Warning and above. These levels are deliberately set in code rather
than accepting arbitrary Serilog sinks from configuration. The legacy Logging
configuration does not control this Serilog pipeline.

Every event passes through an allowlist projection before either sink receives it.
Logs contain stable controller/action names, server-generated request IDs, status,
duration, process/instance IDs, safe Identity result codes, exception type/method
names, and SQL error numbers/state/class. They omit exception messages, raw stack
traces, SQL text, connection strings, email addresses, user IDs, IP addresses,
URLs/query strings, passwords, cookies, tokens and arbitrary scope properties.
Unknown-user and incorrect-password attempts share the same Rejected outcome.
The existing error page request ID now matches the logged request ID.

Files roll daily and at 2 MiB, retain at most eight files and expire after seven
days when rolling occurs: approximately 16 MiB plus the final event/overlap during
recycling. Writes are synchronous and unbuffered; there is no background queue.
Logging exceptions are contained; an unavailable file sink falls back to console
with a safe LoggingUnavailable event. Sink failures emit a fixed console notice,
rate limited to once per minute. These measures cannot recover a forcibly killed
worker or guarantee writes when the host denies disk access. Missing Stopped
events can indicate abrupt termination, but are not proof of an out-of-memory kill.

## Build and deploy

1. Run `npm ci`, then `npm run css:prod` and commit the generated CSS and manifest
   with the logging source/configuration/package changes.
2. Run `dotnet build "ILLiasu Local Mall.csproj" -c Release` and
   `dotnet publish "ILLiasu Local Mall.csproj" -c Release -p:SkipCssBuild=true`.
   The CSS freshness check still runs. Publish includes the Serilog assemblies and
   IIS web.config; it must not include App_Data/logs, tests or database scripts.
3. Redeploy the approved Git branch through MonsterASP.NET Git Build. Preserve
   Production environment, HTTPS and the existing private database configuration.
   Do not enable the developer exception page or execute provisioning commands.
4. Grant the website worker write access to App_Data/logs using hosting controls
   if necessary. A successful local test cannot establish the host's permissions.

## Production verification and recovery

1. Restart the website from the hosting panel, then request the homepage and login
   page over HTTPS. In WebFTP open the application root (the directory containing
   the DLL and web.config), then App_Data/logs. Refresh and download the latest
   app*.jsonl file. Do not place downloaded logs in wwwroot or commit them.
2. Check ApplicationStarting and ApplicationStarted, followed by HttpRequestCompleted
   with successful status codes. Open `/App_Data/logs/<actual filename>` over HTTPS;
   it must return 404. If it is accessible, stop verification and correct host
   static-file configuration before retaining diagnostics there.
3. Submit one failed login without logging its input. With reachable SQL and no
   matching user, expect IdentityOutcome/Rejected and an ordinary HTTP outcome.
   A database failure instead produces safe SQL diagnostics and the generic error
   page. Match its request ID to UnhandledRequestException/HttpRequestCompleted.
   Do not create accounts or modify database records for this check.
4. Perform a controlled restart and compare instance/process IDs and shutdown
   events. Correlate timestamps (JSON timestamps are UTC) with IIS worker events.
   If termination persists, give the host the event ID, timestamp and sanitized
   diagnostics; ask whether memory limits, recycling or rapid-fail protection fired.
5. If no application file appears, inspect panel debug/stdout capture temporarily
   for LoggingUnavailable or LoggingWriteFailure. Check the application root,
   directory permissions and disk quota. Correct access/path configuration and
   restart; do not weaken authentication or expose a diagnostic HTTP endpoint.
6. After verification, disable temporary ASP.NET Core debug/stdout capture to avoid
   a second unbounded stream. Native stdout rotation is outside this application's
   file limits. Retain/download needed evidence before redeploying; confirm whether
   Git Deploy preserves App_Data because host cleanup can remove runtime files.

MonsterASP.NET instructions: [ASP.NET Core debug logging](https://help.monsterasp.net/troubleshooting/logs/aspnet-core-debug-logging)
and [website logs](https://help.monsterasp.net/troubleshooting/logs/website-logs).

## Local verification

`dotnet run --project tests/LoggingSmoke/LoggingSmoke.csproj -c Release` exercises
the actual MVC Production pipeline plus privacy canaries, failed Identity outcomes,
closed-loopback SQL failures, exception handling, private-file 404s, graceful
lifecycle events, file rotation and unavailable/throwing sinks. Its temporary SQL
target is localhost port 1; it never connects to MonsterASP.NET. Test files live
under obj and are excluded from publish.
