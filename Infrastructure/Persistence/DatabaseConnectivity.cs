using Microsoft.Data.SqlClient;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

public enum DatabaseAvailability { Available, Missing, Unavailable, Unknown }
public sealed record DatabaseProbe(DatabaseAvailability Availability, string Detail);

public static class DatabaseConnectivity
{
    public static bool MayInspectMissingDatabase(IEnumerable<int> errors) =>
        errors.Any(number => number is 4060 or 911) && !errors.Any(number => number is 18456 or 18452);

    public static DatabaseProbe ClassifyMetadata(string? state, bool canViewAllDatabases) => state switch
    {
        "ONLINE" => new(DatabaseAvailability.Available, "Database exists; target login/access still needs verification."),
        not null => new(DatabaseAvailability.Unavailable, "Database exists but is not ONLINE; creation is forbidden."),
        null when canViewAllDatabases => new(DatabaseAvailability.Missing, "Database absence confirmed with full server metadata visibility."),
        _ => new(DatabaseAvailability.Unknown, "Cannot prove database absence with the current metadata permissions; creation is forbidden.")
    };

    public static async Task<DatabaseProbe> ProbeAsync(SqlConnectionStringBuilder target, CancellationToken ct)
    {
        await using var connection = new SqlConnection(target.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            return new(DatabaseAvailability.Available, "Target database connection succeeded.");
        }
        catch (SqlException error) when (MayInspectMissingDatabase(error.Errors.Cast<SqlError>().Select(e => e.Number)))
        {
            // 4060 also means access denied. Only a successful privileged metadata query
            // can establish absence; no network/authentication/TLS error triggers creation.
            var masterTarget = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master", Pooling = false };
            await using var master = new SqlConnection(masterTarget.ConnectionString);
            await master.OpenAsync(ct);
            var result = await InspectMasterAsync(master, target.InitialCatalog, ct);
            return result.Availability == DatabaseAvailability.Available
                ? new(DatabaseAvailability.Unavailable, "Database exists but the target connection failed; check database login/access.") : result;
        }
    }

    internal static async Task<DatabaseProbe> InspectMasterAsync(SqlConnection master, string database, CancellationToken ct)
    {
        await using var command = master.CreateCommand();
        command.CommandText = "SELECT (SELECT state_desc FROM sys.databases WHERE name = @database), " +
            // VIEW ANY DATABASE alone can still hide OFFLINE databases. Require
            // permission that exposes those too before interpreting no row as absent.
            "CASE WHEN IS_SRVROLEMEMBER('sysadmin') = 1 OR HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY DATABASE') = 1 " +
            "OR HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE DATABASE') = 1 THEN 1 ELSE 0 END;";
        command.Parameters.AddWithValue("@database", database);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return ClassifyMetadata(reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetInt32(1) == 1);
    }

    internal static async Task CreateAuthorizedLocalAsync(SqlConnectionStringBuilder target, CancellationToken ct)
    {
        var masterTarget = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var master = new SqlConnection(masterTarget.ConnectionString);
        await master.OpenAsync(ct);
        // No transaction: SQL Server CREATE DATABASE cannot execute inside one.
        await using var command = master.CreateCommand();
        command.CommandText = "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = 'ILLiasu.LocalDatabaseCreation', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 15000; IF @r < 0 THROW 50002, 'Provisioning lock unavailable.', 1;";
        await command.ExecuteNonQueryAsync(ct);
        var current = await InspectMasterAsync(master, target.InitialCatalog, ct);
        if (current.Availability == DatabaseAvailability.Missing)
        {
            using var identifiers = new SqlCommandBuilder();
            command.CommandText = "CREATE DATABASE " + identifiers.QuoteIdentifier(target.InitialCatalog) + ";";
            await command.ExecuteNonQueryAsync(ct);
        }
        else if (current.Availability != DatabaseAvailability.Available)
            throw new ProvisioningSafetyException(current.Detail);
        // Pooling=false guarantees session lock release on disconnect, even on failure.
    }
}
