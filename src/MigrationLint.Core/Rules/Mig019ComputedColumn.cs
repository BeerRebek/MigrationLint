using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG019 — A stored (persisted) computed column is added to an existing table. The value must be
/// calculated and written for every existing row, which rewrites the table under a lock. (A virtual
/// computed column is metadata-only and is not flagged.)
/// </summary>
public sealed class Mig019ComputedColumn : RuleBase
{
    public override string Id => "MIG019";
    public override string Title => "Stored computed column added (forces a table rewrite)";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind is not (OperationKind.AddColumn or OperationKind.AlterColumn) ||
            ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        // Only persisted (stored) computed columns rewrite the table; virtual ones don't.
        if (op.Column is not { ComputedColumnSql: not null, IsStored: true })
        {
            return None;
        }

        return One(op, ctx,
            $"Column '{op.Table}.{op.Name}' is a stored computed column. Its value is calculated and " +
            "written for every existing row, rewriting the table under a lock.",
            "  1. Add the column as a virtual (non-stored) computed column if reads allow it, or\n" +
            "  2. Add a plain column, backfill the computed value in batches, then switch reads.",
            DefaultSeverity);
    }
}
