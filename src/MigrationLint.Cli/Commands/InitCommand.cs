using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Cli.Commands;

/// <summary>Scaffolds a starter <c>migrationlint.json</c> (with the detected provider) so a repo can adopt config quickly.</summary>
public static class InitCommand
{
    public static int Run(ParsedArgs args, TextWriter stdout, TextWriter stderr)
    {
        var scanPath = Path.GetFullPath(args.FirstPositional ?? ".");
        var target = Path.Combine(scanPath, ConfigLoader.FileName);

        if (File.Exists(target))
        {
            stderr.WriteLine($"{ConfigLoader.FileName} already exists at {target}. Not overwriting.");
            return 2;
        }

        var provider = ProviderDetector.AutoDetect(scanPath);
        var providerName = provider switch
        {
            Provider.PostgreSql => "postgres",
            Provider.SqlServer => "sqlserver",
            Provider.MySql => "mysql",
            Provider.Sqlite => "sqlite",
            _ => "postgres",
        };

        var detected = provider == Provider.Unknown ? " // TODO: set your provider" : $" // auto-detected";

        var content =
$@"{{
  ""$schema"": ""https://raw.githubusercontent.com/BeerRebek/MigrationLint/main/schema/migrationlint.schema.json"",
  ""provider"": ""{providerName}"",{detected}
  ""failOn"": ""error"",
  ""deploymentStrategy"": ""rolling"",
  ""rules"": {{
    // Override any rule: ""error"" | ""warning"" | ""info"" | ""off""
    // ""MIG012"": ""off""
  }},
  ""options"": {{
    ""maxOperationsPerMigration"": 10,
    ""smallTables"": [],
    ""smallTableRowThreshold"": 10000
  }}
}}
";
        try
        {
            File.WriteAllText(target, content);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not write {target}: {ex.Message}");
            return 2;
        }

        stdout.WriteLine($"Created {target}");
        stdout.WriteLine("Run 'migrationlint check' to lint your migrations.");
        return 0;
    }
}
