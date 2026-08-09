using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG008 — Unique constraint or unique index added: full table scan under lock.</summary>
public sealed class Mig008UniqueConstraint : RuleBase
{
    public override string Id => "MIG008";
    public override string Title => "Unique constraint or unique index added";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        var triggers = op.Kind == OperationKind.AddUniqueConstraint ||
                       (op.Kind == OperationKind.CreateIndex && op.IsUnique);
        if (!triggers)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        var columns = op.Columns.Count > 0 ? string.Join(", ", op.Columns) : "columns";

        return One(op, ctx,
            $"Adding a unique constraint to '{op.Table}({columns})'. This scans the entire " +
            "table under lock and fails if duplicate values already exist.",
            "  1. Check for duplicates first:\n" +
            $"       SELECT {columns}, COUNT(*) FROM \"{op.Table}\"\n" +
            $"       GROUP BY {columns} HAVING COUNT(*) > 1;\n" +
            "  2. Resolve any duplicates.\n" +
            "  3. PostgreSQL — build the unique index concurrently, then attach it:\n" +
            $"       CREATE UNIQUE INDEX CONCURRENTLY ix ON \"{op.Table}\" ({columns});\n" +
            $"       ALTER TABLE \"{op.Table}\" ADD CONSTRAINT c UNIQUE USING INDEX ix;",
            DefaultSeverity);
    }
}
