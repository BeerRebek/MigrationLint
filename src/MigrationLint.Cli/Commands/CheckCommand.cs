using System.Diagnostics;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Formatting;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Cli.Commands;

public static class CheckCommand
{
    public static int Run(ParsedArgs args, TextWriter stdout, TextWriter stderr)
    {
        var scanPath = Path.GetFullPath(args.FirstPositional ?? ".");

        LintConfig config;
        try
        {
            config = ResolveConfig(args, scanPath);
        }
        catch (ConfigException ex)
        {
            stderr.WriteLine($"Configuration error: {ex.Message}");
            return 2;
        }

        var provider = ResolveProvider(args, config, scanPath);
        if (provider == Provider.Unknown)
        {
            stderr.WriteLine("warning: could not determine database provider; provider-specific rules will be skipped. " +
                             "Set --provider or 'provider' in migrationlint.json.");
        }

        var files = MigrationDiscovery.DiscoverFiles(scanPath, config.MigrationsPath);

        if (args.Flag("changed-only"))
        {
            files = FilterToChanged(files, args.Option("base") ?? "main", scanPath, stderr);
        }

        if (files.Count == 0)
        {
            stderr.WriteLine("No migration files found.");
            return 3;
        }

        var (migrations, skipped, unmapped) = MigrationDiscovery.LoadAll(files, config.Baseline);

        if (unmapped.Count > 0)
        {
            stderr.WriteLine($"note: unmapped migrationBuilder methods encountered (skipped): {string.Join(", ", unmapped)}");
        }

        if (args.Option("small-rows") is { } smallRows && int.TryParse(smallRows, out var threshold))
        {
            config = config with { Options = config.Options with { SmallTableRowThreshold = threshold } };
        }

        var rowCounts = ResolveRowCounts(args, provider, migrations, stderr);
        var report = new RuleEngine().Run(migrations, provider, config, skipped, rowCounts);

        if (DetectSnapshotDrift(files, config) is { } drift)
        {
            report = report with { Violations = report.Violations.Append(drift).ToArray() };
        }

        report = ApplyCliFilters(args, report);

        var output = Render(args, report, scanPath);
        WriteOutput(args, output, stdout);

        return report.MaxSeverity >= config.FailOn && config.FailOn != Severity.Off ? 1 : 0;
    }

    /// <summary>MIG018 — compares the newest migration's target model with the ModelSnapshot to catch drift.</summary>
    private static Violation? DetectSnapshotDrift(IReadOnlyList<string> files, LintConfig config)
    {
        var severity = config.Rules.TryGetValue("MIG018", out var s) ? s : Severity.Warning;
        if (severity == Severity.Off || files.Count == 0)
        {
            return null;
        }

        var newest = files[files.Count - 1];
        var designer = newest.Substring(0, newest.Length - ".cs".Length) + ".Designer.cs";
        var dir = Path.GetDirectoryName(newest) ?? ".";
        string? snapshot;
        try
        {
            snapshot = Directory.GetFiles(dir, "*ModelSnapshot.cs").FirstOrDefault();
        }
        catch
        {
            return null;
        }

        if (snapshot is null || !File.Exists(designer))
        {
            return null;
        }

        string snapshotSource, designerSource;
        try
        {
            snapshotSource = File.ReadAllText(snapshot);
            designerSource = File.ReadAllText(designer);
        }
        catch
        {
            return null;
        }

        if (!SnapshotDrift.HasDrift(snapshotSource, designerSource))
        {
            return null;
        }

        return new Violation
        {
            RuleId = "MIG018",
            Category = RuleCategory.Hygiene,
            Severity = severity,
            MigrationId = Path.GetFileNameWithoutExtension(newest),
            Source = new SourceSpan(snapshot, 1, 1),
            Target = Path.GetFileName(snapshot),
            Message = $"'{Path.GetFileName(snapshot)}' does not match the newest migration's target model. " +
                      "A migration was likely committed without updating the snapshot (a common merge-conflict " +
                      "bug) — the next generated migration will be wrong.",
            SafeAlternative = "Regenerate the snapshot so it reflects every migration:\n" +
                              "  1. Resolve the merge so the ModelSnapshot includes all applied migrations, or\n" +
                              "  2. Re-run 'dotnet ef migrations add' after removing the conflicting one.\n" +
                              "Verify with: dotnet ef migrations has-pending-model-changes",
        };
    }

    private static IReadOnlyDictionary<string, long>? ResolveRowCounts(
        ParsedArgs args, Provider provider, IReadOnlyList<MigrationIr> migrations, TextWriter stderr)
    {
        if (args.Option("connection") is not { } connectionString || connectionString.Length == 0)
        {
            return null;
        }

        var tables = migrations
            .SelectMany(m => m.UpOperations)
            .SelectMany(o => new[] { o.Table, o.Name })
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return TableStats.Query(provider, connectionString, tables, stderr);
    }

    private static LintConfig ResolveConfig(ParsedArgs args, string scanPath)
    {
        var configPath = args.Option("config") ?? ConfigLoader.Discover(scanPath);
        var config = configPath is not null ? ConfigLoader.Load(configPath) : new LintConfig();

        if (args.Option("baseline") is { } baseline)
        {
            config = config with { Baseline = baseline };
        }

        if (args.Option("fail-on") is { } failOn)
        {
            config = config with { FailOn = ConfigLoader.ParseFailOn(failOn, args.Option("config") ?? "cli") };
        }

        if (args.Option("deployment-strategy") is { } strategy)
        {
            config = config with { DeploymentStrategy = ParseStrategy(strategy) };
        }

        return config;
    }

    private static DeploymentStrategy ParseStrategy(string value) =>
        value.ToLowerInvariant() switch
        {
            "rolling" => DeploymentStrategy.Rolling,
            "bluegreen" => DeploymentStrategy.BlueGreen,
            "maintenance" => DeploymentStrategy.MaintenanceWindow,
            _ => throw new ConfigException($"unknown deployment strategy '{value}'."),
        };

    private static Provider ResolveProvider(ParsedArgs args, LintConfig config, string scanPath)
    {
        if (args.Option("provider") is { } p)
        {
            return ProviderDetector.FromString(p);
        }

        if (config.Provider is { } configured)
        {
            return configured;
        }

        return ProviderDetector.AutoDetect(scanPath);
    }

    private static LintReport ApplyCliFilters(ParsedArgs args, LintReport report)
    {
        IEnumerable<Violation> violations = report.Violations;

        if (args.Option("rules") is { } only)
        {
            var set = new HashSet<string>(only.Split(','), StringComparer.OrdinalIgnoreCase);
            violations = violations.Where(v => set.Contains(v.RuleId));
        }

        if (args.Option("exclude-rules") is { } exclude)
        {
            var set = new HashSet<string>(exclude.Split(','), StringComparer.OrdinalIgnoreCase);
            violations = violations.Where(v => !set.Contains(v.RuleId));
        }

        if (args.Option("category") is { } category)
        {
            var wanted = ParseCategory(category);
            violations = violations.Where(v => v.Category == wanted);
        }

        var filtered = violations.ToArray();
        return filtered.Length == report.Violations.Count
            ? report
            : report with { Violations = filtered };
    }

    private static RuleCategory ParseCategory(string value) =>
        value.ToLowerInvariant() switch
        {
            "dataloss" => RuleCategory.DataLoss,
            "failure" => RuleCategory.MigrationFailure,
            "locking" => RuleCategory.Locking,
            "hygiene" => RuleCategory.Hygiene,
            _ => throw new ConfigException($"unknown category '{value}'."),
        };

    private static string Render(ParsedArgs args, LintReport report, string scanPath)
    {
        var cwd = Directory.GetCurrentDirectory();
        string Relativize(string path)
        {
            var rel = Path.GetRelativePath(cwd, path);
            return rel.StartsWith("..", StringComparison.Ordinal) ? path : rel;
        }

        var format = args.Option("format") ?? "console";
        return format.ToLowerInvariant() switch
        {
            "console" => new ConsoleFormatter(UseColor(args), Relativize).Format(report),
            "github" => new GitHubFormatter(Relativize).Format(report),
            "sarif" => new SarifFormatter(Relativize).Format(report),
            "json" => new JsonFormatter(Relativize).Format(report),
            _ => throw new ConfigException($"unknown format '{format}'."),
        };
    }

    private static bool UseColor(ParsedArgs args)
    {
        if (args.Flag("no-color"))
        {
            return false;
        }

        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null)
        {
            return false;
        }

        return !Console.IsOutputRedirected;
    }

    private static void WriteOutput(ParsedArgs args, string output, TextWriter stdout)
    {
        if (args.Option("output") is { } file)
        {
            File.WriteAllText(file, output);
        }
        else
        {
            stdout.Write(output);
        }
    }

    private static IReadOnlyList<string> FilterToChanged(
        IReadOnlyList<string> files, string baseRef, string scanPath, TextWriter stderr)
    {
        try
        {
            var psi = new ProcessStartInfo("git", $"diff --name-only --diff-filter=A {baseRef}...HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = scanPath,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return files;
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var changed = output
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Select(l => Path.GetFullPath(Path.Combine(scanPath, l)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return files.Where(f => changed.Contains(Path.GetFullPath(f))).ToArray();
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"warning: --changed-only failed ({ex.Message}); checking all migrations.");
            return files;
        }
    }
}
