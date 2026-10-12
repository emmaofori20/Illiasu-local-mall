using System.Reflection;
using System.Text;
using ILLiasu_Local_Mall.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ILLiasu_Local_Mall.Infrastructure.Persistence;

/// <summary>Offline, add-only fallback using the same approved definitions as application seeding.</summary>
public static class ReferenceDataSql
{
    public static string Generate()
    {
        static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
        var migrations = typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(ApplicationDbContext))
            .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id != null).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (migrations.Length == 0) throw new ProvisioningSafetyException("No migration chain found for reference SQL prerequisites.");
        var sql = new StringBuilder("""
            -- Generated offline from AppRoles and UnitReferenceData.Definitions.
            -- Apply ONLY after the reviewed schema script, to the confirmed dbo target.
            -- No users, credentials or schema changes. Existing definitions are preserved.
            SET QUOTED_IDENTIFIER ON;
            SET ANSI_NULLS ON;
            SET ANSI_PADDING ON;
            SET ANSI_WARNINGS ON;
            SET ARITHABORT ON;
            SET CONCAT_NULL_YIELDS_NULL ON;
            SET NUMERIC_ROUNDABORT OFF;
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRY
                BEGIN TRANSACTION;
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = 'ILLiasu.DatabaseProvisioning',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @lockResult < 0 THROW 50002, 'Provisioning lock unavailable.', 1;
                IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
                    THROW 50003, 'Apply reviewed schema migrations before reference data.', 1;
            """);
        sql.AppendLine();
        foreach (var id in migrations)
            sql.AppendLine($"    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = {Literal(id!)}) THROW 50003, 'Required migration history missing.', 1;");
        foreach (var role in AppRoles.All)
        {
            sql.AppendLine($"    IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = {Literal(role.ToUpperInvariant())})");
            sql.AppendLine($"        INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(nvarchar(36), NEWID()), {Literal(role)}, {Literal(role.ToUpperInvariant())}, CONVERT(nvarchar(36), NEWID()));");
        }
        foreach (var unit in UnitReferenceData.Definitions)
        {
            sql.AppendLine($"    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = {Literal(unit.Code)})");
            sql.AppendLine($"        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), {Literal(unit.Code)}, {Literal(unit.Name)}, {Literal(unit.Code)}, {(int)unit.Kind}, {unit.Precision}, 1);");
        }
        sql.AppendLine("""
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
            """);
        return sql.ToString();
    }
}
