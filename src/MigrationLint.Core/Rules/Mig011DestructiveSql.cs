using System.Text.RegularExpressions;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG011 — Destructive raw SQL. Raw SQL bypasses every other check, so scan it for
/// obviously destructive statements. (SQL-injection detection is deliberately not done —
/// migrations do not take user input.)
/// </summary>
public sealed class Mig011DestructiveSql : RuleBase
{
    public override string Id => "MIG011";
    public override string Title => "Destructive raw SQL";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.DataLoss;

    private static readonly Regex DropLike = new(
        @"\bDROP\s+(TABLE|COLUMN|INDEX|CONSTRAINT|SCHEMA)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Truncate = new(@"\bTRUNCATE\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeleteNoWhere = new(
        @"\bDELETE\s+FROM\b(?![\s\S]*?\bWHERE\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UpdateNoWhere = new(
        @"\bUPDATE\b(?![\s\S]*?\bWHERE\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.RawSql || op.RawSql is not { } sql)
        {
            return None;
        }

        var matched = Match(sql);
        if (matched is null)
        {
            return None;
        }

        return One(op, ctx,
            $"Raw SQL in '{ctx.MigrationId}' contains a potentially destructive statement " +
            $"({matched}). Raw SQL bypasses every other check in this tool.",
            "  1. Confirm the statement is scoped (a WHERE clause where applicable).\n" +
            "  2. Test against a production-sized copy.\n" +
            "  3. If intentional, suppress with a justification so reviewers see the intent:\n" +
            "       [SuppressMigrationLint(\"<why this is safe>\", \"MIG011\")]",
            DefaultSeverity);
    }

    private static string? Match(string sql)
    {
        if (DropLike.IsMatch(sql))
        {
            return "DROP";
        }

        if (Truncate.IsMatch(sql))
        {
            return "TRUNCATE";
        }

        if (DeleteNoWhere.IsMatch(sql))
        {
            return "DELETE without WHERE";
        }

        if (UpdateNoWhere.IsMatch(sql))
        {
            return "UPDATE without WHERE";
        }

        return null;
    }
}
