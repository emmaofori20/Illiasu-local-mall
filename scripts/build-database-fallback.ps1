$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    # Schema generation remains the separately guarded EF offline procedure.
    $schemaPath = 'docs/deployment/illiasu-production-idempotent.sql'
    if (!(Test-Path -LiteralPath $schemaPath)) { throw 'Generate the reviewed EF schema script first.' }
    dotnet run --project 'ILLiasu Local Mall.csproj' -c Release --no-launch-profile -- --database-provision --export-reference-sql docs/deployment/illiasu-reference-data-idempotent.sql
    if ($LASTEXITCODE -ne 0) { throw 'Reference SQL export failed.' }
    $header = @'
:ON ERROR EXIT
:setvar ExpectedServer "__CONFIRM_SERVER__"
:setvar ExpectedDatabase "__CONFIRM_DATABASE__"
-- Review and replace only the two non-secret confirmations above before execution.
-- Requires SQLCMD mode. Stops on first error; never selects/creates a database.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
IF N'$(ExpectedServer)' = N'__CONFIRM_SERVER__' OR N'$(ExpectedDatabase)' = N'__CONFIRM_DATABASE__'
    THROW 50004, 'Explicit server/database confirmation required.', 1;
IF CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) <> N'$(ExpectedServer)' OR DB_NAME() <> N'$(ExpectedDatabase)'
    THROW 50004, 'Connected server/database does not match confirmations.', 1;
IF SCHEMA_NAME() <> N'dbo'
    THROW 50004, 'dbo default schema required.', 1;
GO
'@
    $schema = Get-Content -LiteralPath $schemaPath -Raw
    $reference = Get-Content -LiteralPath 'docs/deployment/illiasu-reference-data-idempotent.sql' -Raw
    $combinedPath = Join-Path $projectRoot 'docs/deployment/illiasu-production-provisioning.sql'
    [IO.File]::WriteAllText($combinedPath, $header + "`n" + $schema + "`n" + $reference)
    Write-Output 'Combined SQLCMD fallback generated locally; no database connection made.'
} finally { Pop-Location }
