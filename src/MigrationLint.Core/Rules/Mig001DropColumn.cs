using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG001 — Column dropped. Breaks old instances mid rolling deploy.</summary>
public sealed class Mig001DropColumn : RuleBase
{
    public override string Id => "MIG001";
    public override string Title => "Column dropped";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.DataLoss;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.DropColumn || ctx.IsNewTable(op.Table))
        {
            return None;
        }

        // Downgrade to Warning when not a rolling deploy.
        var severity = ctx.Strategy == DeploymentStrategy.Rolling ? Severity.Error : Severity.Warning;

        return One(op, ctx,
            $"Dropping column '{op.Table}.{op.Name}'. During a rolling deployment, instances " +
            "still running the previous version will continue to write to this column and will fail.",
            "Drop columns across two releases:\n" +
            "  Release 1 — stop reading and writing the column in application code.\n" +
            "              Mark the property [NotMapped] or remove it from the entity.\n" +
            "  Release 2 — after Release 1 is fully rolled out, drop the column.\n\n" +
            "If the data may be needed, rename to a quarantine name first and drop later.",
            severity);
    }
}
