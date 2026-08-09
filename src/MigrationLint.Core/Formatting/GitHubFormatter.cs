using System.Text;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Formatting;

/// <summary>GitHub Actions workflow-command annotations (PRD §11.2).</summary>
public sealed class GitHubFormatter
{
    private readonly Func<string, string> _relativize;

    public GitHubFormatter(Func<string, string>? relativize = null) =>
        _relativize = relativize ?? (s => s);

    public string Format(LintReport report)
    {
        var sb = new StringBuilder();
        foreach (var v in report.Violations)
        {
            var level = v.Severity switch
            {
                Severity.Error => "error",
                Severity.Warning => "warning",
                _ => "notice",
            };

            var file = _relativize(v.Source.FilePath);
            var message = Escape(v.Message);
            sb.Append($"::{level} file={file},line={v.Source.Line},title={v.RuleId}::{message}")
              .Append('\n');
        }

        return sb.ToString();
    }

    // GitHub workflow-command escaping for message data.
    private static string Escape(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
}
