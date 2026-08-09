using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG009 — Foreign key added without NOT VALID (PostgreSQL validates every row under lock).</summary>
public sealed class Mig009ForeignKeyNotValid : RuleBase
{
    public override string Id => "MIG009";
    public override string Title => "Foreign key added without NOT VALID (PostgreSQL)";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddForeignKey)
        {
            return None;
        }

        if (ctx.Provider != Provider.PostgreSql)
        {
            return None;
        }

        if (ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        return One(op, ctx,
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
            DefaultSeverity);
    }
}
