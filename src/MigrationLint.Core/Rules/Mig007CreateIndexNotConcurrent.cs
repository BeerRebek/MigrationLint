using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG007 — Index created without a concurrent/online option. The flagship lock rule:
/// no equivalent exists in any other .NET tool.
/// </summary>
public sealed class Mig007CreateIndexNotConcurrent : RuleBase
{
    public override string Id => "MIG007";
    public override string Title => "Index created without concurrent/online option";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.CreateIndex)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        var column = op.Columns.Count > 0 ? op.Columns[0] : "column";

        switch (ctx.Provider)
        {
            case Provider.PostgreSql:
                if (ProviderAnnotations.IsTruthy(op, ProviderAnnotations.NpgsqlCreatedConcurrently))
                {
                    return None;
                }

                return One(op, ctx,
                    $"Index '{op.Name}' on '{op.Table}' is created without CONCURRENTLY. " +
                    "PostgreSQL blocks writes to the table for the entire index build.",
                    "  migrationBuilder.CreateIndex(\n" +
                    $"          name: \"{op.Name}\", table: \"{op.Table}\", column: \"{column}\")\n" +
                    "      .Annotation(\"Npgsql:CreatedConcurrently\", true);\n\n" +
                    "CREATE INDEX CONCURRENTLY cannot run inside a transaction, so this index\n" +
                    "must be the only operation in its migration, with:\n\n" +
                    "  protected override bool SuppressTransaction => true;",
                    DefaultSeverity);

            case Provider.SqlServer:
                if (ProviderAnnotations.IsTruthy(op, ProviderAnnotations.SqlServerOnline))
                {
                    return None;
                }

                return One(op, ctx,
                    $"Index '{op.Name}' on '{op.Table}' is created without ONLINE = ON. " +
                    "SQL Server holds a shared table lock and blocks writes for the duration.",
                    "  migrationBuilder.CreateIndex(...)\n" +
                    "      .Annotation(\"SqlServer:Online\", true);\n\n" +
                    "ONLINE = ON requires Enterprise edition or Azure SQL. On Standard edition,\n" +
                    "schedule the index build in a maintenance window and suppress this rule\n" +
                    "with a justification.",
                    DefaultSeverity);

            // MySQL builds indexes online by default (InnoDB, 5.6+). Sqlite/Unknown are skipped.
            default:
                return None;
        }
    }
}
