using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG015 — Integer type widened, forcing a full table rewrite. Even though it's not data loss,
/// changing e.g. int → bigint changes the on-disk size, so the engine rewrites every row under a
/// lock. The classic case: a table that outgrew 32-bit ids. (Narrowing is covered by MIG005.)
/// </summary>
public sealed class Mig015WideningRewrite : RuleBase
{
    public override string Id => "MIG015";
    public override string Title => "Integer type widened (forces a table rewrite)";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    private static readonly IReadOnlyDictionary<string, int> IntegerRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["byte"] = 1,
            ["short"] = 2,
            ["int"] = 3,
            ["long"] = 4,
        };

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AlterColumn || ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        if (op.Column?.ClrType is not { } newClr || op.OldColumn?.ClrType is not { } oldClr)
        {
            return None;
        }

        if (!IntegerRank.TryGetValue(oldClr, out var oldRank) ||
            !IntegerRank.TryGetValue(newClr, out var newRank) ||
            newRank <= oldRank)
        {
            return None;
        }

        return One(op, ctx,
            $"Column '{op.Table}.{op.Name}' is being widened ({oldClr} → {newClr}). The storage size " +
            "changes, so the table is rewritten row-by-row while holding a lock — slow on a large table.",
            "  1. Add a new column with the wider type.\n" +
            "  2. Backfill in batches (a background job, not this migration) and dual-write from the app.\n" +
            "  3. Switch reads to the new column, then drop the old one in a later release.\n\n" +
            "This is the safe path for the classic 'ran out of 32-bit ids' migration.",
            DefaultSeverity);
    }
}
