using MigrationLint.Core.Formatting;
using MigrationLint.Core.Rules;

namespace MigrationLint.Cli.Commands;

public static class ListRulesCommand
{
    public static int Run(TextWriter stdout)
    {
        stdout.WriteLine("MigrationLint rules:");
        stdout.WriteLine();
        foreach (var rule in RuleCatalog.All)
        {
            var sev = FormatHelpers.SeverityLabel(rule.DefaultSeverity).PadRight(8);
            var cat = FormatHelpers.CategoryLabel(rule.Category).PadRight(9);
            stdout.WriteLine($"  {rule.Id}  {sev} {cat} {rule.Title}");
        }

        stdout.WriteLine();
        stdout.WriteLine("Try the differentiating rules first:  migrationlint check --category locking");
        return 0;
    }
}
