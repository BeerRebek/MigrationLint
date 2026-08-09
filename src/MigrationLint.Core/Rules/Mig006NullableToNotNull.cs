using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG006 — Nullable column made NOT NULL: fails if any existing row holds NULL.</summary>
public sealed class Mig006NullableToNotNull : RuleBase
{
    public override string Id => "MIG006";
    public override string Title => "Nullable column made NOT NULL";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.MigrationFailure;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        // An empty table has no rows to hold NULL, so the tightening can't fail.
        if (op.Kind != OperationKind.AlterColumn || ctx.IsNewTable(op.Table) || ctx.IsEmptyTable(op.Table))
        {
            return None;
        }

        if (op.OldColumn?.IsNullable != true || op.Column?.IsNullable != false)
        {
            return None;
        }

        return One(op, ctx,
            $"Column '{op.Table}.{op.Name}' is being changed from nullable to NOT NULL. " +
            "This fails if any existing row holds NULL.",
            "  1. Backfill NULLs in a prior deployment.\n" +
            $"  2. Verify:  SELECT COUNT(*) FROM \"{op.Table}\" WHERE \"{op.Name}\" IS NULL;\n" +
            "  3. Apply the NOT NULL change once the count is zero.\n\n" +
            "PostgreSQL — add a CHECK (col IS NOT NULL) constraint as NOT VALID first,\n" +
            "validate it without blocking writes, then set NOT NULL.",
            DefaultSeverity);
    }
}
