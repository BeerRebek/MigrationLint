using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG002 — Table dropped. Irreversible data destruction.</summary>
public sealed class Mig002DropTable : RuleBase
{
    public override string Id => "MIG002";
    public override string Title => "Table dropped";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.DataLoss;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.DropTable || ctx.IsNewTable(op.Table ?? op.Name))
        {
            return None;
        }

        return One(op, ctx,
            $"Dropping table '{op.Table ?? op.Name}'. This is irreversible and destroys all data in it.",
            "  1. Confirm no application code or report references the table.\n" +
            "  2. Take an explicit backup or export of the data.\n" +
            "  3. Rename the table (e.g. \"Orders_deprecated_20260809\") in one release.\n" +
            "  4. Drop it in a later release after a soak period.",
            DefaultSeverity);
    }
}
