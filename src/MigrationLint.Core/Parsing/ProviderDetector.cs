using MigrationLint.Core.Model;

namespace MigrationLint.Core.Parsing;

/// <summary>
/// Resolves the database provider from (in priority order) an explicit override,
/// the ModelSnapshot annotation prefixes, then nearby csproj package references.
/// </summary>
public static class ProviderDetector
{
    public static Provider FromString(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "npgsql" => Provider.PostgreSql,
            "sqlserver" or "mssql" => Provider.SqlServer,
            "mysql" or "pomelo" => Provider.MySql,
            "sqlite" => Provider.Sqlite,
            _ => Provider.Unknown,
        };

    /// <summary>Auto-detects the provider by scanning snapshot annotations and csproj references under the root.</summary>
    public static Provider AutoDetect(string scanRoot)
    {
        var fromSnapshot = FromSnapshots(scanRoot);
        if (fromSnapshot != Provider.Unknown)
        {
            return fromSnapshot;
        }

        return FromProjects(scanRoot);
    }

    private static Provider FromSnapshots(string root)
    {
        foreach (var file in SafeFiles(root, "*ModelSnapshot.cs"))
        {
            var text = TryRead(file);
            if (text is null)
            {
                continue;
            }

            var provider = FromAnnotationText(text);
            if (provider != Provider.Unknown)
            {
                return provider;
            }
        }

        return Provider.Unknown;
    }

    public static Provider FromAnnotationText(string text)
    {
        if (text.Contains("Npgsql:"))
        {
            return Provider.PostgreSql;
        }

        if (text.Contains("SqlServer:"))
        {
            return Provider.SqlServer;
        }

        if (text.Contains("MySql:") || text.Contains("Pomelo"))
        {
            return Provider.MySql;
        }

        if (text.Contains("Sqlite:"))
        {
            return Provider.Sqlite;
        }

        return Provider.Unknown;
    }

    private static Provider FromProjects(string root)
    {
        foreach (var file in SafeFiles(root, "*.csproj"))
        {
            var text = TryRead(file);
            if (text is null)
            {
                continue;
            }

            if (text.Contains("Npgsql.EntityFrameworkCore.PostgreSQL"))
            {
                return Provider.PostgreSql;
            }

            if (text.Contains("Microsoft.EntityFrameworkCore.SqlServer"))
            {
                return Provider.SqlServer;
            }

            if (text.Contains("Pomelo.EntityFrameworkCore.MySql") || text.Contains("MySql.EntityFrameworkCore"))
            {
                return Provider.MySql;
            }

            if (text.Contains("Microsoft.EntityFrameworkCore.Sqlite"))
            {
                return Provider.Sqlite;
            }
        }

        return Provider.Unknown;
    }

    private static IEnumerable<string> SafeFiles(string root, string pattern)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var f in files)
        {
            var normalized = f.Replace('\\', '/');
            if (normalized.Contains("/bin/") || normalized.Contains("/obj/"))
            {
                continue;
            }

            yield return f;
        }
    }

    private static string? TryRead(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch
        {
            return null;
        }
    }
}
