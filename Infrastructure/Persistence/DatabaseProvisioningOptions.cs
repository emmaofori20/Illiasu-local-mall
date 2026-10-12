using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.Data.SqlClient;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

public sealed class ProvisioningSafetyException(string message) : Exception(message);

public sealed record DatabaseProvisioningOptions(bool Apply, bool AllowProduction,
    bool CreateLocalDatabase, bool SeedReferenceData, bool SeedOnly,
    string? ConfirmServer, string? ConfirmDatabase)
{
    public static DatabaseProvisioningOptions Parse(string[] args)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        string? server = null, database = null;
        var allowed = new[] { "--database-provision", "--check", "--apply", "--allow-production",
            "--create-local-database", "--seed-reference-data", "--seed-only" };
        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            if (!flags.Add(flag)) throw new ProvisioningSafetyException("Duplicate command option.");
            if (flag is "--confirm-server" or "--confirm-database")
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--"))
                    throw new ProvisioningSafetyException("Target confirmation requires a value.");
                if (flag == "--confirm-server") server = args[i]; else database = args[i];
            }
            else if (!allowed.Contains(flag))
                throw new ProvisioningSafetyException("Unknown or incompatible provisioning option. Use --database-provision --help.");
        }
        if (!flags.Contains("--database-provision") || (flags.Contains("--check") && flags.Contains("--apply")))
            throw new ProvisioningSafetyException("Choose --database-provision with either --check (default) or --apply.");
        var apply = flags.Contains("--apply");
        if (!apply && flags.Overlaps(["--create-local-database", "--seed-reference-data", "--seed-only"]))
            throw new ProvisioningSafetyException("Creation and seeding require --apply.");
        if (flags.Contains("--seed-only") && (!flags.Contains("--seed-reference-data") || flags.Contains("--create-local-database")))
            throw new ProvisioningSafetyException("--seed-only requires --seed-reference-data and cannot create a database.");
        return new(apply, flags.Contains("--allow-production"), flags.Contains("--create-local-database"),
            flags.Contains("--seed-reference-data"), flags.Contains("--seed-only"), server, database);
    }

    // Called before any database access, including a read-only Production check.
    public SqlConnectionStringBuilder Validate(IHostEnvironment environment, string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new ProvisioningSafetyException("DefaultConnection must be configured privately.");
        SqlConnectionStringBuilder target;
        try { target = new(connection); }
        catch (ArgumentException) { throw new ProvisioningSafetyException("DefaultConnection is malformed; its value has not been logged."); }
        if (string.IsNullOrWhiteSpace(target.DataSource) || string.IsNullOrWhiteSpace(target.InitialCatalog)
            || target.DataSource.Any(char.IsControl) || target.InitialCatalog.Any(char.IsControl)
            || target.InitialCatalog.Length > 128 || target.AttachDBFilename.Length != 0 || target.UserInstance
            || new[] { "master", "model", "msdb", "tempdb" }.Contains(target.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new ProvisioningSafetyException("An explicit application server/database is required; system/attached databases are forbidden.");
        if (environment.IsDevelopment())
        {
            try { IdentityProvisioning.RequireLocalDevelopment(environment, connection); }
            catch (InvalidOperationException) { throw new ProvisioningSafetyException("Development provisioning requires the approved Emmanuel / IlliasuLocalMallDb Windows-authenticated target."); }
            if (AllowProduction) throw new ProvisioningSafetyException("--allow-production cannot be used in Development.");
        }
        else if (environment.IsProduction())
        {
            if (!AllowProduction) throw new ProvisioningSafetyException("Production requires explicit --allow-production, including for checks.");
            if (CreateLocalDatabase) throw new ProvisioningSafetyException("Production database creation is forbidden; provision it in the hosting Control Panel.");
            if (target.TrustServerCertificate || target.Encrypt == SqlConnectionEncryptOption.Optional)
                throw new ProvisioningSafetyException("Production requires encryption with a trusted server certificate.");
        }
        else throw new ProvisioningSafetyException("Only guarded Development or explicitly opted-in Production is supported.");
        if (Apply || environment.IsProduction())
        {
            if (!string.Equals(ConfirmServer, target.DataSource, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ConfirmDatabase, target.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new ProvisioningSafetyException("Confirmations must match the configured server endpoint and database exactly.");
        }
        return target;
    }
}
