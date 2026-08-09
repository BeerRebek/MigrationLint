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
    public LintConfig Config { get; init; } = LintConfig.Default;

    public bool IsNewTable(string? table) =>
        table is not null && TablesCreatedInThisMigration.Contains(table);

    public bool IsSmallTable(string? table) =>
        table is not null && SmallTables.Contains(table);

    /// <summary>Resolves the effective severity for a rule, honoring config overrides.</summary>
    public Severity SeverityFor(string ruleId, Severity fallback) =>
        Config.Rules.TryGetValue(ruleId, out var overridden) ? overridden : fallback;
}
