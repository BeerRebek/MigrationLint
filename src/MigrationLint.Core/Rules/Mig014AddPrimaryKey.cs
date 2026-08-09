using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG014 — Primary key added to an existing table. PostgreSQL builds a unique index and sets
/// NOT NULL under an exclusive lock; SQL Server rewrites/reorders the whole table for a clustered PK.
/// </summary>
public sealed class Mig014AddPrimaryKey : RuleBase
{
    public override string Id => "MIG014";
    public override string Title => "Primary key added to an existing table";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddPrimaryKey)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        var columns = op.Columns.Count > 0 ? string.Join(", ", op.Columns) : "columns";

        return One(op, ctx,
            $"Adding a primary key to existing table '{op.Table}({columns})'. This scans the table " +
            "under an exclusive lock (PostgreSQL builds a unique index; SQL Server rewrites the table " +
            "for a clustered key) and fails if duplicates or NULLs exist.",
            "  1. Ensure the columns are already NOT NULL and unique.\n" +
            "  2. PostgreSQL — build the unique index concurrently, then attach the primary key:\n" +
            $"       CREATE UNIQUE INDEX CONCURRENTLY pk_ix ON \"{op.Table}\" ({columns});\n" +
            $"       ALTER TABLE \"{op.Table}\" ADD CONSTRAINT \"{op.Name}\" PRIMARY KEY USING INDEX pk_ix;\n" +
            "  3. SQL Server — prefer a nonclustered PK, or schedule the rebuild in a maintenance window.",
            DefaultSeverity);
    }
}
