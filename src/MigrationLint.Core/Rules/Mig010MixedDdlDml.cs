using System.Text.RegularExpressions;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>MIG010 — Schema changes mixed with data changes in one migration (extends DDL-lock transaction).</summary>
public sealed class Mig010MixedDdlDml : MigrationLevelRuleBase
{
    public override string Id => "MIG010";
    public override string Title => "Schema changes mixed with data changes";
    public override Severity DefaultSeverity => Severity.Warning;
    public override RuleCategory Category => RuleCategory.Locking;

    private static readonly Regex Dml = new(@"\b(INSERT|UPDATE|DELETE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx)
    {
        var hasDdl = migration.UpOperations.Any(IsSchemaChange);
        var dataOp = migration.UpOperations.FirstOrDefault(IsDataChange);

        if (!hasDdl || dataOp is null)
        {
            return None;
        }

        return One(migration, ctx, dataOp.Source, migration.Id,
            $"Migration '{migration.Id}' mixes schema changes with data modification. The data " +
            "operation extends the transaction that holds DDL locks.",
            "  1. Apply the schema change in its own migration.\n" +
            "  2. Move the data change to a separate migration, or to an idempotent\n" +
            "     background job that runs after deployment.",
            DefaultSeverity);
    }

    // Structured data ops (InsertData/UpdateData/DeleteData) and DML raw SQL are data changes.
    private static bool IsDataChange(MigrationOperationIr op) =>
        op.Kind is OperationKind.InsertData or OperationKind.UpdateData or OperationKind.DeleteData
        || (op.Kind == OperationKind.RawSql && op.RawSql is { } sql && Dml.IsMatch(sql));

    // A schema change is a structural DDL operation — not a data op, not raw SQL.
    private static bool IsSchemaChange(MigrationOperationIr op) =>
        op.Kind is not (OperationKind.RawSql
            or OperationKind.InsertData
            or OperationKind.UpdateData
            or OperationKind.DeleteData);
}
