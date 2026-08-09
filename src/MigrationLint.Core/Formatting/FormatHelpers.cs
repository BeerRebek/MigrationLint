using MigrationLint.Core.Model;

namespace MigrationLint.Core.Formatting;

public static class FormatHelpers
{
    public static string SeverityLabel(Severity severity) =>
        severity switch
        {
            Severity.Error => "error",
            Severity.Warning => "warning",
            Severity.Info => "info",
            _ => "off",
        };

    public static string CategoryLabel(RuleCategory category) =>
        category switch
        {
            RuleCategory.DataLoss => "dataloss",
            RuleCategory.MigrationFailure => "failure",
            RuleCategory.Locking => "locking",
            RuleCategory.Hygiene => "hygiene",
            _ => "other",
        };

    /// <summary>Wraps text to a width, preserving no existing newlines (single paragraph).</summary>
    public static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var line = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }

    public static string Indent(string text, string indent)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join(Environment.NewLine, lines.Select(l => l.Length == 0 ? l : indent + l));
    }
}
