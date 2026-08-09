using System.Text;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Formatting;

/// <summary>
/// Markdown report suitable for posting as a PR summary comment. One table, grouped by severity,
/// with rule ids linked to their docs.
/// </summary>
public sealed class MarkdownFormatter
{
    private readonly Func<string, string> _relativize;

    public MarkdownFormatter(Func<string, string>? relativize = null) =>
        _relativize = relativize ?? (s => s);

    public string Format(LintReport report)
    {
        var sb = new StringBuilder();
        sb.Append("## MigrationLint\n\n");

        if (report.Violations.Count == 0)
        {
            sb.Append($"✅ No issues found across {report.MigrationsChecked} migration(s).\n");
            return sb.ToString();
        }

        sb.Append($"**{report.ErrorCount} error(s), {report.WarningCount} warning(s)** across ")
          .Append($"{report.MigrationsChecked} migration(s)");
        if (report.MigrationsSkipped > 0)
        {
            sb.Append($" ({report.MigrationsSkipped} skipped by baseline)");
        }

        sb.Append(".\n\n");
        sb.Append("| | Rule | Location | Issue |\n");
        sb.Append("|---|---|---|---|\n");

        foreach (var v in report.Violations)
        {
            var icon = v.Severity switch
            {
                Severity.Error => "❌",
                Severity.Warning => "⚠️",
                Severity.Info => "ℹ️",
                _ => "",
            };
            var location = $"`{_relativize(v.Source.FilePath)}:{v.Source.Line}`";
            var rule = $"[{v.RuleId}]({v.DocsUrl})";
            sb.Append($"| {icon} | {rule} | {location} | {Escape(v.Target)} — {Escape(FirstSentence(v.Message))} |\n");
        }

        return sb.ToString();
    }

    private static string FirstSentence(string message)
    {
        var dot = message.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 ? message.Substring(0, dot + 1) : message;
    }

    // Keep table cells intact: escape pipes and collapse newlines.
    private static string Escape(string value) =>
        value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
