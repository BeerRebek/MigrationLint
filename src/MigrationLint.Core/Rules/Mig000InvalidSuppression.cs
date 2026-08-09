using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG000 — A [SuppressMigrationLint] attribute was used without a justification.</summary>
public sealed class Mig000InvalidSuppression : MigrationLevelRuleBase
{
    public override string Id => "MIG000";
    public override string Title => "Suppression without justification";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.Hygiene;

    public override IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx)
    {
        if (!migration.HasSuppressionWithoutJustification)
        {
            return None;
        }

        var source = migration.UpOperations.Count > 0
            ? migration.UpOperations[0].Source
            : new SourceSpan(migration.FilePath, 1, 1);

        return One(migration, ctx, source, migration.Id,
            $"Migration '{migration.Id}' uses [SuppressMigrationLint] without a justification. " +
            "A justification is required so reviewers can see why a rule was suppressed.",
            "Provide a justification as the first argument:\n" +
            "  [SuppressMigrationLint(\"<why this is safe>\", \"MIG007\")]",
            DefaultSeverity);
    }
}
