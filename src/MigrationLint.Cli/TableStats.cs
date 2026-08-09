using System.Data;
using Microsoft.Data.SqlClient;
using MigrationLint.Core.Model;
using Npgsql;

namespace MigrationLint.Cli;

/// <summary>
/// Opt-in, read-only live-database awareness. Queries estimate-based row counts for the tables a
/// migration touches so the engine can suppress false positives (empty tables can't fail a NOT NULL
/// add; small tables don't lock long). Never writes, never scans, short timeout, fails soft.
/// </summary>
public static class TableStats
{
    private const int TimeoutSeconds = 10;

    /// <summary>Returns row counts by table name, or an empty map on any failure (with a warning).</summary>
    public static IReadOnlyDictionary<string, long> Query(
        Provider provider,
        string connectionString,
        IReadOnlyCollection<string> tables,
        TextWriter stderr)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (tables.Count == 0)
        {
            return result;
        }

        try
        {
            switch (provider)
            {
                case Provider.PostgreSql:
                    QueryPostgres(connectionString, tables, result);
                    break;
                case Provider.SqlServer:
                    QuerySqlServer(connectionString, tables, result);
                    break;
                default:
                    stderr.WriteLine($"warning: --connection is not supported for provider '{provider}' yet; ignoring live stats.");
                    break;
            }
        }
        catch (Exception ex)
        {
            // Never fail the lint because the database was unreachable — degrade to no stats.
            stderr.WriteLine($"warning: could not read live table stats ({ex.GetType().Name}: {ex.Message}); continuing without them.");
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void QueryPostgres(string cs, IReadOnlyCollection<string> tables, Dictionary<string, long> into)
    {
        using var conn = new NpgsqlConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = TimeoutSeconds;
        // reltuples is a planner estimate maintained by ANALYZE — no table scan.
        cmd.CommandText = @"
            SELECT c.relname, GREATEST(c.reltuples, 0)::bigint
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND c.relname = ANY(@names);";
        var p = cmd.CreateParameter();
        p.ParameterName = "names";
        p.Value = tables.ToArray();
        cmd.Parameters.Add(p);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            into[reader.GetString(0)] = reader.GetInt64(1);
        }
    }

    private static void QuerySqlServer(string cs, IReadOnlyCollection<string> tables, Dictionary<string, long> into)
    {
        using var conn = new SqlConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = TimeoutSeconds;
        // sys.partitions.rows for the heap/clustered index — metadata only, no scan.
        var names = tables.ToArray();
        var placeholders = string.Join(",", names.Select((_, i) => "@t" + i));
        cmd.CommandText = $@"
            SELECT t.name, SUM(p.rows)
            FROM sys.tables t
            JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            WHERE t.name IN ({placeholders})
            GROUP BY t.name;";
        for (var i = 0; i < names.Length; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@t" + i;
            p.DbType = DbType.String;
            p.Value = names[i];
            cmd.Parameters.Add(p);
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            into[reader.GetString(0)] = reader.GetInt64(1);
        }
    }
}
