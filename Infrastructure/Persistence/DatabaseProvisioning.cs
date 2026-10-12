using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

/// <summary>Explicit CLI workflow only. Never called by ordinary web startup.</summary>
public static class DatabaseProvisioning
{
    public const string Help = """
        Database provisioning (no web server is started):
          --database-provision --check                 Read-only check (default)
          --database-provision --apply                 Apply only pending migrations
          --confirm-server <configured endpoint> --confirm-database <configured database>
          --allow-production                          Required for all Production commands
          --create-local-database                     Create only a proven-missing approved local database
          --seed-reference-data                       Add approved roles and reference units after migration
          --seed-only --seed-reference-data --apply    Seed only; refuse pending migrations
          --export-reference-sql <local path>          Generate add-only SQL offline (standalone mode)
        Supply DefaultConnection privately, never as a command argument. No default admin is created.
        """;

    public static void ValidateHistory(IReadOnlyList<string> known, IReadOnlyList<string> applied)
    {
        if (known.Count == 0 || !applied.SequenceEqual(known.Take(applied.Count), StringComparer.Ordinal))
            throw new ProvisioningSafetyException("Migration history is unknown, out of order, or not a prefix of the shipped chain. Reconcile it manually; no changes performed.");
    }

    public static async Task<int> RunAsync(IServiceProvider services, IHostEnvironment environment,
        IConfiguration configuration, string[] args, TextWriter output, CancellationToken ct = default)
    {
        if (args.SequenceEqual(new[] { "--database-provision", "--help" }))
        { await output.WriteLineAsync(Help); return 0; }
        var stage = "validation";
        try
        {
            if (args.Length == 3 && args[0] == "--database-provision" && args[1] == "--export-reference-sql")
            {
                stage = "offline reference SQL export";
                var path = Path.GetFullPath(args[2]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, ReferenceDataSql.Generate(), ct);
                await output.WriteLineAsync("Reference SQL exported locally; no database connection or changes made.");
                return 0;
            }
            var options = DatabaseProvisioningOptions.Parse(args);
            var target = options.Validate(environment, configuration.GetConnectionString("DefaultConnection"));
            await output.WriteLineAsync($"Environment={environment.EnvironmentName}; server={target.DataSource}; database={target.InitialCatalog}; mode={(options.Apply ? "APPLY" : "CHECK")}");
            stage = "connectivity";
            var probe = await DatabaseConnectivity.ProbeAsync(target, ct);
            await output.WriteLineAsync($"Connectivity: {probe.Availability}. {probe.Detail}");
            if (probe.Availability == DatabaseAvailability.Missing)
            {
                if (!options.Apply || !options.CreateLocalDatabase)
                { await output.WriteLineAsync("No database created. Local creation needs --apply --create-local-database; Production needs the hosting Control Panel."); return 3; }
                // Repeat the existing environment/target guard immediately before creation.
                IdentityProvisioning.RequireLocalDevelopment(environment, target.ConnectionString);
                await DatabaseConnectivity.CreateAuthorizedLocalAsync(target, ct);
                await output.WriteLineAsync("Approved local database created (or created concurrently); opening target.");
            }
            else if (probe.Availability != DatabaseAvailability.Available) return 4;

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.OpenConnectionAsync(ct);
            var locked = false;
            try
            {
                // Check connected database and default schema without logging credentials.
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "SELECT DB_NAME(), SCHEMA_NAME();";
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    await reader.ReadAsync(ct);
                    if (!string.Equals(reader.GetString(0), target.InitialCatalog, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(reader.GetString(1), "dbo", StringComparison.Ordinal))
                        throw new ProvisioningSafetyException("Connected database/default schema does not match the required target/dbo schema.");
                }
                if (options.Apply)
                {
                    stage = "migration lock";
                    await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = 'ILLiasu.DatabaseProvisioning', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 15000; IF @r < 0 THROW 50002, 'Provisioning lock unavailable.', 1;", ct);
                    locked = true;
                }
                stage = "migration inspection";
                var known = db.Database.GetMigrations().ToArray();
                var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
                ValidateHistory(known, applied);
                if (db.Database.HasPendingModelChanges())
                    throw new ProvisioningSafetyException("The model differs from the latest migration; generate/review a migration before provisioning.");
                if (applied.Length == 0)
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> '__EFMigrationsHistory';";
                    if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0)
                        throw new ProvisioningSafetyException("Untracked existing tables found without applied EF history. Refusing to adopt or recreate the schema.");
                }
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
                await output.WriteLineAsync($"Applied migrations: {applied.Length}/{known.Length}. Pending: {(pending.Length == 0 ? "none" : string.Join(", ", pending))}");
                if (!options.Apply) { await output.WriteLineAsync("Check complete; no schema or reference data changed."); return 0; }
                if (options.SeedOnly && pending.Length != 0)
                    throw new ProvisioningSafetyException("Seed-only refused: apply the reviewed migrations first.");
                if (!options.SeedOnly && pending.Length != 0)
                {
                    stage = "migration application";
                    await db.Database.MigrateAsync(ct);
                    await output.WriteLineAsync("Pending migrations applied successfully.");
                }
                if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                    throw new ProvisioningSafetyException("Pending migrations remain; reference data was not seeded.");
                if (options.SeedReferenceData)
                {
                    stage = "role seeding";
                    await IdentityProvisioning.SeedRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
                    await output.WriteLineAsync("Approved roles ready; existing roles preserved. No users or credentials created.");
                    stage = "reference unit seeding";
                    var added = await UnitReferenceData.SeedAsync(db, ct);
                    await output.WriteLineAsync($"Reference units: {added} added; existing definitions preserved.");
                }
                await output.WriteLineAsync("Provisioning complete. No database dropped or recreated.");
                return 0;
            }
            finally
            {
                if (locked)
                {
                    try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_releaseapplock @Resource = 'ILLiasu.DatabaseProvisioning', @LockOwner = 'Session';", CancellationToken.None); }
                    catch { SqlConnection.ClearPool((SqlConnection)db.Database.GetDbConnection()); }
                }
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (OperationCanceledException)
        { await output.WriteLineAsync($"Cancelled during {stage}; inspect migration history before retrying."); return 130; }
        catch (ProvisioningSafetyException error)
        { await output.WriteLineAsync($"Refused during {stage}: {error.Message}"); return 2; }
        catch (SqlException error)
        {
            var codes = string.Join(", ", error.Errors.Cast<SqlError>().Select(e => $"{e.Number}/state={e.State}").Distinct());
            await output.WriteLineAsync($"SQL failure during {stage} ({codes}). No connection details logged. Do not infer absence from this failure. Review connectivity/permissions/TLS and history before retrying.");
            return stage == "connectivity" ? 4 : 5;
        }
        catch (Exception error)
        {
            // Provider exception text may contain credentials/connection details; do not print it.
            await output.WriteLineAsync($"Failure during {stage} ({error.GetType().Name}). Check private diagnostics and migration history; no automatic retry or rollback is attempted.");
            return 5;
        }
    }
}
