using System.Text;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Formatting;

/// <summary>Human-readable console output with optional ANSI color (PRD §11.1).</summary>
public sealed class ConsoleFormatter
{
    private const int Width = 76;
    private readonly bool _color;
    private readonly Func<string, string> _relativize;

    public ConsoleFormatter(bool color, Func<string, string>? relativize = null)
    {
        _color = color;
        _relativize = relativize ?? (s => s);
    }

    private const string Reset = "[0m";
    private const string Red = "[31m";
    private const string Yellow = "[33m";
    private const string Blue = "[34m";
    private const string Dim = "[2m";

    public string Format(LintReport report)
    {
        var sb = new StringBuilder();

        foreach (var v in report.Violations)
        {
            WriteViolation(sb, v);
        }

        WriteSummary(sb, report);
        return sb.ToString();
    }

    private void WriteViolation(StringBuilder sb, Violation v)
    {
        var (symbol, color) = v.Severity switch
        {
            Severity.Error => ("✖", Red),
            Severity.Warning => ("⚠", Yellow),
            Severity.Info => ("ℹ", Blue),
            _ => ("✖", Red),
        };

        var sev = FormatHelpers.SeverityLabel(v.Severity);
        var location = $"{_relativize(v.Source.FilePath)}:{v.Source.Line}";
        sb.Append(Paint(color, $"{symbol} {v.RuleId}")).Append("  ")
          .Append(Paint(color, sev.PadRight(7))).Append(' ')
          .Append(Paint(Dim, location))
          .AppendLine();

        var category = $"[{FormatHelpers.CategoryLabel(v.Category)}]";
        sb.Append("  ").Append(v.Target.PadRight(Width - category.Length)).Append(Paint(Dim, category)).AppendLine();
        sb.AppendLine();

        foreach (var line in FormatHelpers.Wrap(v.Message, Width))
        {
            sb.Append("  ").AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("  Safe alternative:");
        sb.AppendLine(FormatHelpers.Indent(v.SafeAlternative, "    "));
        sb.AppendLine();
        sb.Append("  ").Append(Paint(Dim, $"Docs: {v.DocsUrl}")).AppendLine();
        sb.AppendLine();
    }

    private void WriteSummary(StringBuilder sb, LintReport report)
    {
        if (report.Violations.Count == 0)
        {
            sb.Append(Paint(Blue, "No violations found"))
              .Append($" across {report.MigrationsChecked} migration{Plural(report.MigrationsChecked)}");
        }
        else
        {
            var errs = report.ErrorCount;
            var warns = report.WarningCount;
            sb.Append($"{errs} error{Plural(errs)}, {warns} warning{Plural(warns)} across " +
                      $"{report.MigrationsChecked} migration{Plural(report.MigrationsChecked)}");
        }

        if (report.MigrationsSkipped > 0)
        {
            sb.Append($" ({report.MigrationsSkipped} skipped by baseline)");
        }

        sb.Append('.').AppendLine();
    }

    private static string Plural(int n) => n == 1 ? "" : "s";

    private string Paint(string color, string text) => _color ? color + text + Reset : text;
}
