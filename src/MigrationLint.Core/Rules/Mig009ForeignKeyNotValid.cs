using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG009 — Foreign key added without a deferred-validation option. Both PostgreSQL and
/// SQL Server validate every existing row under a lock when the constraint is added the
/// default way; each has a way to add it fast and validate separately.
/// </summary>
public sealed class Mig009ForeignKeyNotValid : RuleBase
{
    public override string Id => "MIG009";
    public override string Title => "Foreign key added without deferred validation";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddForeignKey)
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
                $"Adding foreign key '{op.Name}' on '{op.Table}'. PostgreSQL validates every " +
                "existing row while holding a lock on both tables.",
                "Split into two migrations:\n" +
                "  1. Add the constraint as NOT VALID (fast, no scan):\n" +
                "       migrationBuilder.Sql(\n" +
                $"         \"ALTER TABLE \\\"{op.Table}\\\" ADD CONSTRAINT \\\"{op.Name}\\\" \" +\n" +
                "         \"FOREIGN KEY (...) REFERENCES ... NOT VALID;\");\n" +
                "  2. Validate it in a later migration (does not block writes):\n" +
                "       migrationBuilder.Sql(\n" +
                $"         \"ALTER TABLE \\\"{op.Table}\\\" VALIDATE CONSTRAINT \\\"{op.Name}\\\";\");",
                DefaultSeverity),

            Provider.SqlServer => One(op, ctx,
                $"Adding foreign key '{op.Name}' on '{op.Table}'. SQL Server validates every " +
                "existing row (WITH CHECK) while holding a schema-modify lock on the table.",
                "Split into two migrations:\n" +
                "  1. Add the constraint WITH NOCHECK (fast, no scan):\n" +
                "       migrationBuilder.Sql(\n" +
                $"         \"ALTER TABLE [{op.Table}] WITH NOCHECK ADD CONSTRAINT [{op.Name}] \" +\n" +
                "         \"FOREIGN KEY (...) REFERENCES ...;\");\n" +
                "  2. Validate it in a later migration (does not block writes):\n" +
                "       migrationBuilder.Sql(\n" +
                $"         \"ALTER TABLE [{op.Table}] WITH CHECK CHECK CONSTRAINT [{op.Name}];\");\n\n" +
                "Note: a NOCHECK constraint is 'not trusted' until validated, so run step 2 before\n" +
                "relying on the optimizer to use it.",
                DefaultSeverity),

            // MySQL/Sqlite/Unknown: no dialect-correct deferred-validation guidance to give.
            _ => None,
        };
    }
}
