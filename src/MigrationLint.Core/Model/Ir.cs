namespace MigrationLint.Core.Model;

public enum OperationKind
{
    Unknown,
    CreateTable,
    DropTable,
    RenameTable,
    AddColumn,
    DropColumn,
    AlterColumn,
    RenameColumn,
    CreateIndex,
    DropIndex,
    AddForeignKey,
    AddUniqueConstraint,
    RawSql,
    InsertData,
    UpdateData,
    DeleteData,
    AddPrimaryKey,
    AddCheckConstraint,
}

public enum Provider
{
    Unknown,
    PostgreSql,
    SqlServer,
    MySql,
    Sqlite,
}

public enum Severity
{
    Off,
    Info,
    Warning,
    Error,
}

public enum RuleCategory
{
    DataLoss,
    MigrationFailure,
    Locking,
    Hygiene,
}

public enum DeploymentStrategy
{
    Rolling,
    BlueGreen,
    MaintenanceWindow,
}

public sealed record SourceSpan(string FilePath, int Line, int Column);

public sealed record ColumnInfo
{
    public string? ClrType { get; init; }
    public string? StoreType { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public bool? IsNullable { get; init; }
    public bool HasDefault { get; init; }
    public string? DefaultValue { get; init; }
    public string? DefaultValueSql { get; init; }
    public string? ComputedColumnSql { get; init; }
    public bool? IsStored { get; init; }
}

public sealed record MigrationOperationIr
{
    public OperationKind Kind { get; init; }
    public SourceSpan Source { get; init; } = new("", 0, 0);
    public string? Table { get; init; }
    public string? Name { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public ColumnInfo? Column { get; init; }
    public ColumnInfo? OldColumn { get; init; }
    public bool IsUnique { get; init; }
    public string? RawSql { get; init; }
    public IReadOnlyDictionary<string, string?> Annotations { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Human-readable target of the operation, e.g. "Orders(Notes)" or "Orders".</summary>
    public string Target =>
        Columns.Count > 0 && Table is not null
            ? $"{Table}({string.Join(", ", Columns)})"
            : Name is not null && Table is not null && Kind is not OperationKind.RenameTable and not OperationKind.DropTable and not OperationKind.CreateTable
                ? $"{Table}.{Name}"
                : Table ?? Name ?? "(unknown)";
}

public sealed record MigrationIr
{
    public string Id { get; init; } = "";
    public string FilePath { get; init; } = "";
    public IReadOnlyList<MigrationOperationIr> UpOperations { get; init; } = Array.Empty<MigrationOperationIr>();
    public IReadOnlyList<MigrationOperationIr> DownOperations { get; init; } = Array.Empty<MigrationOperationIr>();
    public IReadOnlyList<string> SuppressedRules { get; init; } = Array.Empty<string>();
    public string? SuppressionJustification { get; init; }

    /// <summary>True when a suppression attribute with a justification but no explicit rule ids is present.</summary>
    public bool SuppressesAllRules { get; init; }

    /// <summary>True when the migration overrides <c>SuppressTransaction</c> to return true.</summary>
    public bool SuppressesTransaction { get; init; }

    /// <summary>True when a <c>[SuppressMigrationLint]</c> attribute is present but has no justification (drives MIG000).</summary>
    public bool HasSuppressionWithoutJustification { get; init; }
}

public sealed record Violation
{
    public string RuleId { get; init; } = "";
    public RuleCategory Category { get; init; }
    public Severity Severity { get; init; }
    public string MigrationId { get; init; } = "";
    public SourceSpan Source { get; init; } = new("", 0, 0);
    public string Target { get; init; } = "";
    public string Message { get; init; } = "";
    public string SafeAlternative { get; init; } = "";

    public string DocsUrl =>
        $"https://github.com/BeerRebek/MigrationLint/blob/main/docs/rules/{RuleId}.md";
}

public sealed record LintReport(
    IReadOnlyList<Violation> Violations,
    int MigrationsChecked,
    int MigrationsSkipped)
{
    public Severity MaxSeverity =>
        Violations.Count == 0 ? Severity.Off : Violations.Max(v => v.Severity);

    public int ErrorCount => Violations.Count(v => v.Severity == Severity.Error);

    public int WarningCount => Violations.Count(v => v.Severity == Severity.Warning);
}
