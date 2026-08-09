using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG013 — Check constraint added without deferred validation. Like a foreign key, both
/// PostgreSQL and SQL Server validate every existing row under a lock when it is added the
/// default way.
/// </summary>
public sealed class Mig013CheckConstraint : RuleBase
{
    public override string Id => "MIG013";
    public override string Title => "Check constraint added without deferred validation";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddCheckConstraint)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        return ctx.Provider switch
        {
            Provider.PostgreSql => One(op, ctx,
                $"Adding check constraint '{op.Name}' on '{op.Table}'. PostgreSQL validates every " +
                "existing row while holding a lock on the table.",
                "Split into two migrations:\n" +
                "  1. Add the constraint as NOT VALID (fast, no scan):\n" +
                $"       migrationBuilder.Sql(\"ALTER TABLE \\\"{op.Table}\\\" ADD CONSTRAINT \\\"{op.Name}\\\" CHECK (...) NOT VALID;\");\n" +
                "  2. Validate it in a later migration (does not block writes):\n" +
                $"       migrationBuilder.Sql(\"ALTER TABLE \\\"{op.Table}\\\" VALIDATE CONSTRAINT \\\"{op.Name}\\\";\");",
                DefaultSeverity),

            Provider.SqlServer => One(op, ctx,
                $"Adding check constraint '{op.Name}' on '{op.Table}'. SQL Server validates every " +
                "existing row (WITH CHECK) while holding a schema-modify lock on the table.",
                "Split into two migrations:\n" +
                "  1. Add the constraint WITH NOCHECK (fast, no scan):\n" +
                $"       migrationBuilder.Sql(\"ALTER TABLE [{op.Table}] WITH NOCHECK ADD CONSTRAINT [{op.Name}] CHECK (...);\");\n" +
                "  2. Validate it later (does not block writes):\n" +
                $"       migrationBuilder.Sql(\"ALTER TABLE [{op.Table}] WITH CHECK CHECK CONSTRAINT [{op.Name}];\");",
                DefaultSeverity),

            _ => None,
        };
    }
}
