using MigrationLint.Core.Model;

namespace MigrationLint.Core.Parsing;

/// <summary>
/// Discovers EF migration files on disk and parses them into <see cref="MigrationIr"/>.
/// This is the one place that touches the filesystem — rules never do.
/// </summary>
public static class MigrationDiscovery
{
    private static readonly string[] ExcludedDirs = { "bin", "obj", "node_modules" };

    /// <summary>Finds candidate migration files under <paramref name="scanRoot"/>.</summary>
    public static IReadOnlyList<string> DiscoverFiles(string scanRoot, string? migrationsPath)
    {
        if (!string.IsNullOrEmpty(migrationsPath))
        {
            string full = Path.IsPathRooted(migrationsPath)
                ? migrationsPath!
                : Path.Combine(scanRoot, migrationsPath!);
            return CollectMigrations(Directory.Exists(full) ? new[] { full } : Array.Empty<string>());
        }

        // Locate directories that contain a *ModelSnapshot.cs, then scan those.
        var snapshotDirs = EnumerateFiles(scanRoot)
            .Where(f => f.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetDirectoryName)
            .Where(d => d is not null)
            .Select(d => d!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Fall back to the whole tree if no snapshot exists (hand-written migrations).
        var roots = snapshotDirs.Length > 0 ? snapshotDirs : new[] { scanRoot };
        return CollectMigrations(roots);
    }

    private static IReadOnlyList<string> CollectMigrations(IEnumerable<string> roots)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            foreach (var file in EnumerateFiles(root))
            {
                if (file.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (LooksLikeMigration(file))
                {
                    files.Add(file);
                }
            }
        }

        // Sort by filename (EF IDs are timestamp-prefixed, so filename order is chronological).
        return files.OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
    }

    private static bool LooksLikeMigration(string file)
    {
        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch
        {
            return false;
        }

        // Cheap pre-filter before the parser confirms the base type.
        return text.Contains(": Migration") || text.Contains(":Migration") || text.Contains("Migration\n") || text.Contains("migrationBuilder");
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in SafeEnumerate(root))
        {
            yield return file;
        }
    }

    private static IEnumerable<string> SafeEnumerate(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] subdirs;
            string[] files;
            try
            {
                subdirs = Directory.GetDirectories(dir);
                files = Directory.GetFiles(dir, "*.cs");
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (!ExcludedDirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    stack.Push(sub);
                }
            }
        }
    }

    /// <summary>Parses discovered files, applying the baseline filter. Aggregates unmapped method names.</summary>
    public static (IReadOnlyList<MigrationIr> Migrations, int Skipped, IReadOnlyList<string> Unmapped) LoadAll(
        IReadOnlyList<string> files,
        string? baseline)
    {
        var migrations = new List<MigrationIr>();
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (baseline is not null && string.CompareOrdinal(id, baseline) <= 0)
            {
                skipped++;
                continue;
            }

            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            if (MigrationFileParser.TryParse(file, source, out var migration, out var fileUnmapped))
            {
                migrations.Add(migration);
                foreach (var m in fileUnmapped)
                {
                    unmapped.Add(m);
                }
            }
        }

        return (migrations, skipped, unmapped.ToArray());
    }
}
