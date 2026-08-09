using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG017 — An index is marked <c>CreatedConcurrently</c> but the migration doesn't override
/// <c>SuppressTransaction</c>. <c>CREATE INDEX CONCURRENTLY</c> cannot run inside a transaction, so
/// this migration fails at runtime. This is the mistake people make right after adopting MIG007.
/// </summary>
public sealed class Mig017SuppressTransaction : MigrationLevelRuleBase
{
    public override string Id => "MIG017";
    public override string Title => "CONCURRENTLY index without SuppressTransaction";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.MigrationFailure;

    public override IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx)
    {
        if (migration.SuppressesTransaction)
        {
            return None;
        }

        var concurrent = migration.UpOperations.FirstOrDefault(
            o => o.Kind == OperationKind.CreateIndex &&
                 ProviderAnnotations.IsTruthy(o, ProviderAnnotations.NpgsqlCreatedConcurrently));

        if (concurrent is null)
        {
            return None;
        }

        return One(migration, ctx, concurrent.Source, concurrent.Target,
            $"Index '{concurrent.Name}' is created CONCURRENTLY, but migration '{migration.Id}' does " +
            "not override SuppressTransaction. CREATE INDEX CONCURRENTLY cannot run inside a " +
            "transaction, so this migration will fail when applied.",
            "Add the override to the migration class, and keep this index as its only operation:\n\n" +
            "    protected override bool SuppressTransaction => true;",
            DefaultSeverity);
    }
}
