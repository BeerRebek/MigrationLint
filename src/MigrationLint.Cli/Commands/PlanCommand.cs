using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Cli.Commands;

/// <summary>
/// `migrationlint plan` — reframes each migration as a safe deployment sequence
/// (expand → migrate → contract) instead of a pass/fail lint.
/// </summary>
public static class PlanCommand
{
    public static int Run(ParsedArgs args, TextWriter stdout, TextWriter stderr)
    {
        var scanPath = Path.GetFullPath(args.FirstPositional ?? ".");
        var configPath = args.Option("config") ?? ConfigLoader.Discover(scanPath);
        var config = configPath is not null ? ConfigLoader.Load(configPath) : new LintConfig();

        var provider = args.Option("provider") is { } p
            ? ProviderDetector.FromString(p)
            : config.Provider ?? ProviderDetector.AutoDetect(scanPath);

        var files = MigrationDiscovery.DiscoverFiles(scanPath, config.MigrationsPath);
        if (files.Count == 0)
        {
            stderr.WriteLine("No migration files found.");
            return 3;
        }

        var (migrations, _, _) = MigrationDiscovery.LoadAll(files, config.Baseline);
        var report = new RuleEngine().Run(migrations, provider, config, skipped: 0);

        foreach (var migration in migrations)
        {
            var rulesByLine = report.Violations
                .Where(v => v.MigrationId == migration.Id)
                .GroupBy(v => v.Source.Line)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<string>)g.Select(v => v.RuleId).Distinct().ToList());

            PrintPlan(stdout, migration.Id, DeploymentPlanner.Plan(migration, rulesByLine));
        }

        return 0;
    }

    private static void PrintPlan(TextWriter o, string migrationId, IReadOnlyList<PlanStep> steps)
    {
        var phases = steps.Select(s => s.Phase).Distinct().OrderBy(p => p).ToList();

        if (steps.Count == 0)
        {
            o.WriteLine($"{migrationId} — no operations to plan.");
            o.WriteLine();
            return;
        }

        if (phases.Count <= 1)
        {
            o.WriteLine($"{migrationId} — safe to deploy in one step.");
            foreach (var s in steps)
            {
                o.WriteLine($"    • {s.Text}{RuleTag(s)}");
            }

            o.WriteLine();
            return;
        }

        o.WriteLine($"{migrationId} — {phases.Count} deploys recommended");
        o.WriteLine();

        var deployNumber = 1;
        foreach (var phase in phases)
        {
            o.WriteLine($"  Deploy {deployNumber} · {DeploymentPlanner.PhaseLabels[phase]}");
            foreach (var s in steps.Where(s => s.Phase == phase))
            {
                o.WriteLine($"    • {s.Text}{RuleTag(s)}");
            }

            o.WriteLine();
            deployNumber++;
        }

        o.WriteLine("  The current migration attempts all of this in one step.");
        o.WriteLine();
    }

    private static string RuleTag(PlanStep s) => s.RuleId is null ? "" : $"   [{s.RuleId}]";
}
