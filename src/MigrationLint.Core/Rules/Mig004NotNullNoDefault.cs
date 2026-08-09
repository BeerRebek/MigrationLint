using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG004 — NOT NULL column added without a default: fails on any non-empty table.</summary>
public sealed class Mig004NotNullNoDefault : RuleBase
{
    public override string Id => "MIG004";
    public override string Title => "NOT NULL column added without a default";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.MigrationFailure;

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddColumn || ctx.IsNewTable(op.Table))
        {
            return None;
        }

        // Silence is correct when the parser cannot see nullability.
        if (op.Column is not { IsNullable: false, HasDefault: false })
        {
            return None;
        }

        return One(op, ctx,
            $"Adding NOT NULL column '{op.Table}.{op.Name}' without a default value. This " +
            "statement fails immediately if the table contains any rows.",
            "Option A — supply a default (single deployment):\n" +
            "  migrationBuilder.AddColumn<string>(\n" +
            $"      name: \"{op.Name}\", table: \"{op.Table}\",\n" +
            "      nullable: false, defaultValue: \"\");\n\n" +
            "Option B — three-step, when a default is not acceptable:\n" +
            "  1. Add the column as nullable.\n" +
            "  2. Backfill existing rows in batches (background job, not this migration).\n" +
            "  3. Once no NULLs remain, alter the column to NOT NULL.",
            DefaultSeverity);
    }
}
