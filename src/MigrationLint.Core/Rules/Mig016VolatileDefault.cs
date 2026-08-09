using System.Text.RegularExpressions;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG016 — Column added with a volatile default. A *constant* default is a fast metadata change on
/// modern PostgreSQL/SQL Server, but a *volatile* one (now(), gen_random_uuid(), NEWID(), …) must be
/// evaluated per row, rewriting the whole table under a lock. A common surprise — people assume all
/// defaults became free.
/// </summary>
public sealed class Mig016VolatileDefault : RuleBase
{
    public override string Id => "MIG016";
    public override string Title => "Column added with a volatile default (forces a table rewrite)";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    private static readonly Regex Volatile = new(
        @"\b(now|current_timestamp|current_date|current_time|clock_timestamp|timeofday|" +
        @"gen_random_uuid|uuid_generate_v\d|random|newid|newsequentialid|getdate|getutcdate|sysdatetime|rand)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AddColumn || ctx.IsNewTable(op.Table) || ctx.IsSmallTable(op.Table))
        {
            return None;
        }

        if (ctx.Provider is not (Provider.PostgreSql or Provider.SqlServer))
        {
            return None;
        }

        if (op.Column?.DefaultValueSql is not { } sql || !Volatile.IsMatch(sql))
        {
            return None;
        }

        return One(op, ctx,
            $"Column '{op.Table}.{op.Name}' is added with a volatile default ({sql.Trim()}). Unlike a " +
            "constant default, this is evaluated for every existing row, rewriting the table under a lock.",
            "  1. Add the column as nullable, with no default.\n" +
            "  2. Backfill the value in batches (a background job, not this migration).\n" +
            "  3. Set the default (and NOT NULL, if needed) once the backfill is complete.",
            DefaultSeverity);
    }
}
