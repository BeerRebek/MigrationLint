using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG020 — Index dropped without CONCURRENTLY on PostgreSQL. A plain <c>DROP INDEX</c> takes an
/// ACCESS EXCLUSIVE lock on the table, briefly blocking all reads and writes.
/// </summary>
public sealed class Mig020DropIndexConcurrently : RuleBase
{
    public override string Id => "MIG020";
    public override string Title => "Index dropped without CONCURRENTLY (PostgreSQL)";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.DropIndex || ctx.Provider != Provider.PostgreSql)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        return One(op, ctx,
            $"Index '{op.Name}' is dropped without CONCURRENTLY. On PostgreSQL a plain DROP INDEX takes " +
            "an ACCESS EXCLUSIVE lock on the table, blocking reads and writes while it runs.",
            "Run the drop concurrently in its own transaction-less migration:\n\n" +
            $"    migrationBuilder.Sql(\"DROP INDEX CONCURRENTLY \\\"{op.Name}\\\";\");\n\n" +
            "    protected override bool SuppressTransaction => true;",
            DefaultSeverity);
    }
}
