using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Tests;

/// <summary>Loads deliberately-bad fixture .cs files (copied to output, never compiled) and runs them.</summary>
public static class TestHarness
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string FixturePath(string namePart)
    {
        var match = Directory.GetFiles(FixturesDir, "*.cs")
            .FirstOrDefault(f => Path.GetFileName(f).Contains(namePart, StringComparison.Ordinal));

        return match ?? throw new FileNotFoundException($"No fixture matching '{namePart}' under {FixturesDir}");
    }

    public static IReadOnlyList<string> AllFixtures() =>
        Directory.GetFiles(FixturesDir, "*.cs").OrderBy(f => f, StringComparer.Ordinal).ToArray();

    public static MigrationIr Parse(string namePart)
    {
        var path = FixturePath(namePart);
        var ok = MigrationFileParser.TryParse(path, File.ReadAllText(path), out var migration, out _);
        if (!ok)
        {
            throw new InvalidOperationException($"Fixture '{namePart}' did not parse as a migration.");
        }

        return migration;
    }

    public static LintReport Run(
        string namePart,
        Provider provider,
        LintConfig? config = null,
        IReadOnlyDictionary<string, long>? rowCounts = null)
    {
        var migration = Parse(namePart);
        return new RuleEngine().Run(new[] { migration }, provider, config ?? new LintConfig(), skipped: 0, rowCounts);
    }
}
