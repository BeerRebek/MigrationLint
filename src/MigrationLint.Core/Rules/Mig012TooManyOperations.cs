using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG012 — Too many operations in one migration: long transaction, held locks, hard partial rollback.</summary>
public sealed class Mig012TooManyOperations : MigrationLevelRuleBase
{
    public override string Id => "MIG012";
    public override string Title => "Too many operations in one migration";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Hygiene;

    public override IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx)
    {
        var count = migration.UpOperations.Count;
        if (count <= ctx.Config.Options.MaxOperationsPerMigration)
        {
            return None;
        }

        var source = migration.UpOperations.Count > 0
            ? migration.UpOperations[0].Source
            : new SourceSpan(migration.FilePath, 1, 1);

        return One(migration, ctx, source, migration.Id,
            $"Migration '{migration.Id}' contains {count} operations. Large migrations run in a " +
            "single long transaction, hold locks longer, and are difficult to roll back partially.",
            "Split into smaller migrations, each independently deployable and reversible.\n" +
            "Order them so every intermediate state is compatible with both the previous\n" +
            "and the next application version.",
            DefaultSeverity);
    }
}
