using MigrationLint.Core.Model;

namespace MigrationLint.Core.Engine;

public sealed record LintOptions
{
    public int MaxOperationsPerMigration { get; init; } = 10;
    public IReadOnlyList<string> SmallTables { get; init; } = Array.Empty<string>();

    /// <summary>With live stats, a table with at most this many rows is treated as small.</summary>
    public int SmallTableRowThreshold { get; init; } = 10_000;
}

/// <summary>
/// Plain configuration data. Deliberately contains no file I/O or JSON: the CLI loads
/// it from <c>migrationlint.json</c>; the analyzer will supply it from
/// <c>.editorconfig</c>. Rules only ever see this through <see cref="LintContext"/>.
/// </summary>
public sealed record LintConfig
{
    public Provider? Provider { get; init; }
    public string? MigrationsPath { get; init; }
    public string? Baseline { get; init; }
    public DeploymentStrategy DeploymentStrategy { get; init; } = DeploymentStrategy.Rolling;
    public Severity FailOn { get; init; } = Severity.Error;

    /// <summary>Per-rule severity overrides. A rule mapped to <see cref="Severity.Off"/> is disabled.</summary>
    public IReadOnlyDictionary<string, Severity> Rules { get; init; } =
        new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase);

    public LintOptions Options { get; init; } = new();

    public static LintConfig Default { get; } = new();
}
