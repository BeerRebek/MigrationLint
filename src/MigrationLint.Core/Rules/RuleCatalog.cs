namespace MigrationLint.Core.Rules;

/// <summary>The full set of rules. Order here is the order used for reporting and docs.</summary>
public static class RuleCatalog
{
    public static IReadOnlyList<IMigrationRule> OperationRules { get; } = new IMigrationRule[]
    {
        // Lock rules first — they are the differentiator (PRD §12 Phase 2).
        new Mig007CreateIndexNotConcurrent(),
        new Mig008UniqueConstraint(),
        new Mig009ForeignKeyNotValid(),
        new Mig001DropColumn(),
        new Mig002DropTable(),
        new Mig003Rename(),
        new Mig004NotNullNoDefault(),
        new Mig005NarrowType(),
        new Mig006NullableToNotNull(),
        new Mig011DestructiveSql(),
        new Mig013CheckConstraint(),
        new Mig014AddPrimaryKey(),
        new Mig015WideningRewrite(),
        new Mig016VolatileDefault(),
        new Mig019ComputedColumn(),
        new Mig020DropIndexConcurrently(),
    };

    public static IReadOnlyList<IMigrationLevelRule> MigrationRules { get; } = new IMigrationLevelRule[]
    {
        new Mig000InvalidSuppression(),
        new Mig010MixedDdlDml(),
        new Mig012TooManyOperations(),
        new Mig017SuppressTransaction(),
    };

    /// <summary>Corpus-level checks that need the whole migration set + snapshot (run by the CLI, not per-op).</summary>
    public static IReadOnlyList<RuleInfo> CorpusRules { get; } = new[]
    {
        new RuleInfo("MIG018", "Migration checked in without a ModelSnapshot update",
            Model.Severity.Warning, Model.RuleCategory.Hygiene),
    };

    /// <summary>All rule ids and metadata, for list-rules / SARIF driver / explain.</summary>
    public static IReadOnlyList<RuleInfo> All { get; } =
        OperationRules.Select(r => new RuleInfo(r.Id, r.Title, r.DefaultSeverity, r.Category))
            .Concat(MigrationRules.Select(r => new RuleInfo(r.Id, r.Title, r.DefaultSeverity, r.Category)))
            .Concat(CorpusRules)
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToArray();
}

public sealed record RuleInfo(
    string Id,
    string Title,
    Model.Severity DefaultSeverity,
    Model.RuleCategory Category);
