using System.Text;
using MigrationLint.Core.Model;
using MigrationLint.Core.Rules;

namespace MigrationLint.Core.Formatting;

/// <summary>SARIF 2.1.0 output for github/codeql-action/upload-sarif (PRD §11.3).</summary>
public sealed class SarifFormatter
{
    private const string Version = "0.1.0-preview";
    private readonly Func<string, string> _relativize;

    public SarifFormatter(Func<string, string>? relativize = null) =>
        _relativize = relativize ?? (s => s);

    public string Format(LintReport report)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"$schema\": \"https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json\",\n");
        sb.Append("  \"version\": \"2.1.0\",\n");
        sb.Append("  \"runs\": [\n");
        sb.Append("    {\n");
        sb.Append("      \"tool\": {\n");
        sb.Append("        \"driver\": {\n");
        sb.Append("          \"name\": \"MigrationLint\",\n");
        sb.Append($"          \"version\": {Json.Str(Version)},\n");
        sb.Append("          \"informationUri\": \"https://github.com/BeerRebek/MigrationLint\",\n");
        sb.Append("          \"rules\": [\n");
        WriteRules(sb);
        sb.Append("          ]\n");
        sb.Append("        }\n");
        sb.Append("      },\n");
        sb.Append("      \"results\": [\n");
        WriteResults(sb, report);
        sb.Append("      ]\n");
        sb.Append("    }\n");
        sb.Append("  ]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void WriteRules(StringBuilder sb)
    {
        var rules = RuleCatalog.All;
        for (var i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            var helpUri = $"https://github.com/BeerRebek/MigrationLint/blob/main/docs/rules/{r.Id}.md";
            sb.Append("            {\n");
            sb.Append($"              \"id\": {Json.Str(r.Id)},\n");
            sb.Append($"              \"name\": {Json.Str(r.Id + r.Title.Replace(" ", ""))},\n");
            sb.Append("              \"shortDescription\": { \"text\": ").Append(Json.Str(r.Title)).Append(" },\n");
            sb.Append($"              \"helpUri\": {Json.Str(helpUri)},\n");
            sb.Append("              \"defaultConfiguration\": { \"level\": ").Append(Json.Str(Level(r.DefaultSeverity))).Append(" }\n");
            sb.Append(i == rules.Count - 1 ? "            }\n" : "            },\n");
        }
    }

    private void WriteResults(StringBuilder sb, LintReport report)
    {
        for (var i = 0; i < report.Violations.Count; i++)
        {
            var v = report.Violations[i];
            var uri = _relativize(v.Source.FilePath).Replace('\\', '/');
            sb.Append("        {\n");
            sb.Append($"          \"ruleId\": {Json.Str(v.RuleId)},\n");
            sb.Append($"          \"level\": {Json.Str(Level(v.Severity))},\n");
            sb.Append("          \"message\": { \"text\": ").Append(Json.Str(v.Message)).Append(" },\n");
            sb.Append("          \"locations\": [\n");
            sb.Append("            {\n");
            sb.Append("              \"physicalLocation\": {\n");
            sb.Append("                \"artifactLocation\": { \"uri\": ").Append(Json.Str(uri)).Append(" },\n");
            sb.Append("                \"region\": { \"startLine\": ").Append(Math.Max(1, v.Source.Line)).Append(", \"startColumn\": ").Append(Math.Max(1, v.Source.Column)).Append(" }\n");
            sb.Append("              }\n");
            sb.Append("            }\n");
            sb.Append("          ]\n");
            sb.Append(i == report.Violations.Count - 1 ? "        }\n" : "        },\n");
        }
    }

    private static string Level(Severity severity) =>
        severity switch
        {
            Severity.Error => "error",
            Severity.Warning => "warning",
            _ => "note",
        };
}
