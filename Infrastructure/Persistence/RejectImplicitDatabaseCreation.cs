using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

// EF's migrator can create a missing database. Provisioning permits creation only
// through the separately guarded local path, never as a side effect of Migrate.
public sealed class RejectImplicitDatabaseCreation : DbCommandInterceptor
{
    public static void ValidateSql(string sql)
    {
        if (Regex.IsMatch(sql, @"\bCREATE\s+DATABASE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new ProvisioningSafetyException("Implicit EF database creation is forbidden. Use the approved local creation path or hosting Control Panel.");
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { ValidateSql(command.CommandText); return result; }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { ValidateSql(command.CommandText); return ValueTask.FromResult(result); }
}
