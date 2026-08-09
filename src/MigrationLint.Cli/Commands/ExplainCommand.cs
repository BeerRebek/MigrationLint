using MigrationLint.Core.Formatting;
using MigrationLint.Core.Rules;

namespace MigrationLint.Cli.Commands;

public static class ExplainCommand
{
    public static int Run(ParsedArgs args, TextWriter stdout, TextWriter stderr)
    {
        var id = args.FirstPositional;
        if (id is null)
        {
            stderr.WriteLine("usage: migrationlint explain <MIG004>");
            return 2;
        }

        var rule = RuleCatalog.All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
        {
            stderr.WriteLine($"Unknown rule '{id}'. Run 'migrationlint list-rules' to see all rules.");
            return 2;
        }

        stdout.WriteLine($"{rule.Id} — {rule.Title}");
        stdout.WriteLine($"  Category:         {FormatHelpers.CategoryLabel(rule.Category)}");
        stdout.WriteLine($"  Default severity: {FormatHelpers.SeverityLabel(rule.DefaultSeverity)}");
        stdout.WriteLine($"  Docs:             https://github.com/BeerRebek/MigrationLint/blob/main/docs/rules/{rule.Id}.md");
        return 0;
    }
}
