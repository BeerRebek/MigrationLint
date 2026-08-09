using System.Text.Json;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Cli;

/// <summary>
/// Loads <c>migrationlint.json</c> into a <see cref="LintConfig"/>. Lives in the CLI (not Core)
/// so Core stays free of a System.Text.Json dependency for the netstandard2.0 analyzer target.
/// </summary>
public static class ConfigLoader
{
    public const string FileName = "migrationlint.json";

    /// <summary>Walks upward from <paramref name="startDir"/> to find the config file.</summary>
    public static string? Discover(string startDir)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir));
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Parses a config file. Throws <see cref="ConfigException"/> on malformed input.</summary>
    public static LintConfig Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Could not read config '{path}': {ex.Message}");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Config '{path}' is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var config = new LintConfig();

            if (root.TryGetProperty("provider", out var provider) && provider.ValueKind == JsonValueKind.String)
            {
                config = config with { Provider = ProviderDetector.FromString(provider.GetString()) };
            }

            if (root.TryGetProperty("migrationsPath", out var mp) && mp.ValueKind == JsonValueKind.String)
            {
                config = config with { MigrationsPath = mp.GetString() };
            }

            if (root.TryGetProperty("baseline", out var bl) && bl.ValueKind == JsonValueKind.String)
            {
                config = config with { Baseline = bl.GetString() };
            }

            if (root.TryGetProperty("deploymentStrategy", out var ds) && ds.ValueKind == JsonValueKind.String)
            {
                config = config with { DeploymentStrategy = ParseStrategy(ds.GetString(), path) };
            }

            if (root.TryGetProperty("failOn", out var fo) && fo.ValueKind == JsonValueKind.String)
            {
                config = config with { FailOn = ParseFailOn(fo.GetString(), path) };
            }

            if (root.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase);
                foreach (var rule in rules.EnumerateObject())
                {
                    map[rule.Name] = ParseSeverity(rule.Value.GetString(), path, rule.Name);
                }

                config = config with { Rules = map };
            }

            if (root.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Object)
            {
                var opts = new LintOptions();
                if (options.TryGetProperty("maxOperationsPerMigration", out var max) && max.ValueKind == JsonValueKind.Number)
                {
                    opts = opts with { MaxOperationsPerMigration = max.GetInt32() };
                }

                if (options.TryGetProperty("smallTables", out var st) && st.ValueKind == JsonValueKind.Array)
                {
                    opts = opts with
                    {
                        SmallTables = st.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToArray(),
                    };
                }

                config = config with { Options = opts };
            }

            return config;
        }
    }

    private static DeploymentStrategy ParseStrategy(string? value, string path) =>
        value?.ToLowerInvariant() switch
        {
            "rolling" => DeploymentStrategy.Rolling,
            "bluegreen" => DeploymentStrategy.BlueGreen,
            "maintenance" => DeploymentStrategy.MaintenanceWindow,
            _ => throw new ConfigException($"Config '{path}': unknown deploymentStrategy '{value}'."),
        };

    public static Severity ParseFailOn(string? value, string path) =>
        value?.ToLowerInvariant() switch
        {
            "error" => Severity.Error,
            "warning" => Severity.Warning,
            "none" => Severity.Off,
            _ => throw new ConfigException($"Config '{path}': unknown failOn '{value}'."),
        };

    private static Severity ParseSeverity(string? value, string path, string ruleId) =>
        value?.ToLowerInvariant() switch
        {
            "error" => Severity.Error,
            "warning" => Severity.Warning,
            "info" => Severity.Info,
            "off" => Severity.Off,
            _ => throw new ConfigException($"Config '{path}': rule '{ruleId}' has invalid severity '{value}'."),
        };
}

public sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message)
    {
    }
}
