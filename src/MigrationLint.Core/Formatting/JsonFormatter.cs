using System.Text;
using MigrationLint.Core.Formatting;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Formatting;

/// <summary>Machine-readable JSON output (PRD §11.4).</summary>
public sealed class JsonFormatter
{
    private readonly Func<string, string> _relativize;

    public JsonFormatter(Func<string, string>? relativize = null) =>
        _relativize = relativize ?? (s => s);

    public string Format(LintReport report)
    {
        var sb = new StringBuilder();
        sb.Append('{').Append('\n');
        sb.Append($"  \"migrationsChecked\": {report.MigrationsChecked},\n");
        sb.Append($"  \"migrationsSkipped\": {report.MigrationsSkipped},\n");
        sb.Append($"  \"errorCount\": {report.ErrorCount},\n");
        sb.Append($"  \"warningCount\": {report.WarningCount},\n");
        sb.Append("  \"violations\": [");

        for (var i = 0; i < report.Violations.Count; i++)
        {
            var v = report.Violations[i];
            sb.Append(i == 0 ? "\n" : ",\n");
            sb.Append("    {\n");
            sb.Append($"      \"ruleId\": {Json.Str(v.RuleId)},\n");
            sb.Append($"      \"category\": {Json.Str(FormatHelpers.CategoryLabel(v.Category))},\n");
            sb.Append($"      \"severity\": {Json.Str(FormatHelpers.SeverityLabel(v.Severity))},\n");
            sb.Append($"      \"migrationId\": {Json.Str(v.MigrationId)},\n");
            sb.Append($"      \"file\": {Json.Str(_relativize(v.Source.FilePath))},\n");
            sb.Append($"      \"line\": {v.Source.Line},\n");
            sb.Append($"      \"target\": {Json.Str(v.Target)},\n");
            sb.Append($"      \"message\": {Json.Str(v.Message)},\n");
            sb.Append($"      \"safeAlternative\": {Json.Str(v.SafeAlternative)},\n");
            sb.Append($"      \"docsUrl\": {Json.Str(v.DocsUrl)}\n");
            sb.Append("    }");
        }

        sb.Append(report.Violations.Count > 0 ? "\n  ]\n" : "]\n");
        sb.Append("}\n");
        return sb.ToString();
    }
}
