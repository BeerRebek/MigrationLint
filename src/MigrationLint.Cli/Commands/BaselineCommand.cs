using MigrationLint.Core.Parsing;

namespace MigrationLint.Cli.Commands;

/// <summary>
/// Suggests a baseline: the newest existing migration id. Setting this in config makes the
/// tool adoptable on a mature repo — everything at or before the baseline is skipped.
/// </summary>
public static class BaselineCommand
{
    public static int Run(ParsedArgs args, TextWriter stdout, TextWriter stderr)
    {
        var scanPath = Path.GetFullPath(args.FirstPositional ?? ".");
        var files = MigrationDiscovery.DiscoverFiles(scanPath, null);

        if (files.Count == 0)
        {
            stderr.WriteLine("No migration files found.");
            return 3;
        }

        var newest = Path.GetFileNameWithoutExtension(files[^1]);
        stdout.WriteLine($"Latest migration: {newest}");
        stdout.WriteLine();
        stdout.WriteLine("Add this to migrationlint.json to skip all existing migrations:");
        stdout.WriteLine();
        stdout.WriteLine("  {");
        stdout.WriteLine($"    \"baseline\": \"{newest}\"");
        stdout.WriteLine("  }");
        return 0;
    }
}
