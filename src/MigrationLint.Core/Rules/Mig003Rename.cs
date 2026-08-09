using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG003 — Column or table renamed. Instantly breaks the previous app version.</summary>
public sealed class Mig003Rename : RuleBase
{
    public override string Id => "MIG003";
    public override string Title => "Column or table renamed";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.DataLoss;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind is not (OperationKind.RenameColumn or OperationKind.RenameTable))
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table ?? op.Name))
        {
            return None;
        }

        // Downgrade to Info in a maintenance window (no old version running concurrently).
        var severity = ctx.Strategy == DeploymentStrategy.MaintenanceWindow ? Severity.Info : Severity.Error;

        return One(op, ctx,
            $"Renaming {op.Target}. The rename takes effect instantly and breaks every running " +
            "instance of the previous application version.",
            "Use expand/contract across three releases:\n" +
            "  Release 1 — add the new column, dual-write to both old and new.\n" +
            "  Release 2 — backfill, then switch reads to the new column.\n" +
            "  Release 3 — stop writing the old column, then drop it.",
            severity);
    }
}
