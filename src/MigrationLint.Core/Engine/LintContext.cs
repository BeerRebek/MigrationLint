using MigrationLint.Core.Model;

namespace MigrationLint.Core.Engine;

/// <summary>
/// Everything a rule is allowed to see. Immutable, cheap to construct, no I/O.
/// This is the seam that lets the same rule run under the CLI and under a Roslyn analyzer.
/// </summary>
public sealed record LintContext
{
    public string MigrationId { get; init; } = "";
    public Provider Provider { get; init; }
    public DeploymentStrategy Strategy { get; init; } = DeploymentStrategy.Rolling;
    public IReadOnlyList<MigrationOperationIr> AllOperations { get; init; } = Array.Empty<MigrationOperationIr>();
    public ISet<string> TablesCreatedInThisMigration { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public ISet<string> SmallTables { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Live row counts by table (from <c>--connection</c>). Empty unless opted in.</summary>
    public IReadOnlyDictionary<string, long> RowCounts { get; init; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Row count at or below which a table is treated as small (0 disables the row-count rule).</summary>
    public int SmallTableRowThreshold { get; init; }

    /// <summary>Live NULL counts keyed by "table.column" (from <c>--connection</c>). Empty unless opted in.</summary>
    public IReadOnlyDictionary<string, long> NullCounts { get; init; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public LintConfig Config { get; init; } = LintConfig.Default;

    public bool IsNewTable(string? table) =>
        table is not null && TablesCreatedInThisMigration.Contains(table);

    /// <summary>Known live row count for a table, or null when no live stats are available.</summary>
    public long? RowCount(string? table) =>
        table is not null && RowCounts.TryGetValue(table, out var count) ? count : null;

    /// <summary>True only when live stats confirm the table has zero rows.</summary>
    public bool IsEmptyTable(string? table) => RowCount(table) == 0;

    /// <summary>Known live NULL count for a column, or null when no live stats are available.</summary>
    public long? NullCount(string? table, string? column) =>
        table is not null && column is not null && NullCounts.TryGetValue($"{table}.{column}", out var count)
            ? count
            : null;

    /// <summary>True only when live stats confirm the column has zero NULLs.</summary>
    public bool HasNoNulls(string? table, string? column) => NullCount(table, column) == 0;

    public bool IsSmallTable(string? table) =>
        (table is not null && SmallTables.Contains(table)) ||
        (SmallTableRowThreshold > 0 && RowCount(table) is { } count && count <= SmallTableRowThreshold);

    /// <summary>Resolves the effective severity for a rule, honoring config overrides.</summary>
    public Severity SeverityFor(string ruleId, Severity fallback) =>
        Config.Rules.TryGetValue(ruleId, out var overridden) ? overridden : fallback;
}
