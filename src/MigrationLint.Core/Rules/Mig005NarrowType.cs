using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// MIG005 — Column type narrowed. Unifies what the competitor splits across three
/// overlapping analyzers (length, numeric rank, precision/scale).
/// </summary>
public sealed class Mig005NarrowType : RuleBase
{
    public override string Id => "MIG005";
    public override string Title => "Column type narrowed";
    public override Severity DefaultSeverity => Severity.Error;
    public override RuleCategory Category => RuleCategory.DataLoss;

    private static readonly IReadOnlyDictionary<string, int> NumericRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["byte"] = 1,
            ["short"] = 2,
            ["int"] = 3,
            ["long"] = 4,
            ["decimal"] = 5,
        };

    public override IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx)
    {
        if (op.Kind != OperationKind.AlterColumn || ctx.IsNewTable(op.Table))
        {
            return None;
        }

        if (op.Column is null || op.OldColumn is null)
        {
            return None;
        }

        var narrowing = DescribeNarrowing(op.OldColumn, op.Column);
        if (narrowing is null)
        {
            return None;
        }

        return One(op, ctx,
            $"Column '{op.Table}.{op.Name}' type change is narrowing ({narrowing}). Existing " +
            "values may be truncated, and the table will be rewritten while holding a lock.",
            "  1. Verify no existing value exceeds the new bound.\n" +
            "  2. Add a new column with the target type.\n" +
            "  3. Dual-write from the application, then backfill.\n" +
            "  4. Switch reads to the new column, then drop the old one in a later release.",
            DefaultSeverity);
    }

    private static string? DescribeNarrowing(ColumnInfo old, ColumnInfo @new)
    {
        if (old.MaxLength is { } oldLen && @new.MaxLength is { } newLen && newLen < oldLen)
        {
            return $"maxLength {oldLen} → {newLen}";
        }

        if (old.ClrType is { } oldClr && @new.ClrType is { } newClr &&
            NumericRank.TryGetValue(oldClr, out var oldRank) &&
            NumericRank.TryGetValue(newClr, out var newRank) &&
            newRank < oldRank)
        {
            return $"{oldClr} → {newClr}";
        }

        if (old.Precision is { } oldP && @new.Precision is { } newP && newP < oldP)
        {
            return $"precision {oldP} → {newP}";
        }

        if (old.Scale is { } oldS && @new.Scale is { } newS && newS < oldS)
        {
            return $"scale {oldS} → {newS}";
        }

        return null;
    }
}
