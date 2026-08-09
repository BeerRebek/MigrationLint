using System.Reflection;
using MigrationLint.Cli;
using MigrationLint.Cli.Commands;

var stdout = Console.Out;
var stderr = Console.Error;

if (args.Length == 1 && args[0] is "--version" or "-v")
{
    stdout.WriteLine(Version());
    return 0;
}

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    PrintHelp(stdout);
    return 0;
}

var parsed = ParsedArgs.Parse(args);

try
{
    return parsed.Command switch
    {
        "check" => CheckCommand.Run(parsed, stdout, stderr),
        "baseline" => BaselineCommand.Run(parsed, stdout, stderr),
        "explain" => ExplainCommand.Run(parsed, stdout, stderr),
        "list-rules" => ListRulesCommand.Run(stdout),
        _ => Unknown(parsed.Command, stderr),
    };
}
catch (ConfigException ex)
{
    stderr.WriteLine($"Configuration error: {ex.Message}");
    return 2;
}

static int Unknown(string? command, TextWriter stderr)
{
    stderr.WriteLine($"Unknown command '{command}'. Run 'migrationlint --help'.");
    return 2;
}

static string Version() =>
    Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "0.0.0";

static void PrintHelp(TextWriter stdout)
{
    stdout.WriteLine("MigrationLint — the EF Core migration linter that understands database locks.");
    stdout.WriteLine();
    stdout.WriteLine("Usage:");
    stdout.WriteLine("  migrationlint check [path] [options]");
    stdout.WriteLine("  migrationlint baseline [path]");
    stdout.WriteLine("  migrationlint explain <MIG004>");
    stdout.WriteLine("  migrationlint list-rules");
    stdout.WriteLine("  migrationlint --version");
    stdout.WriteLine();
    stdout.WriteLine("check options:");
    stdout.WriteLine("  --provider <postgres|sqlserver|mysql|sqlite>");
    stdout.WriteLine("  --config <file>              --baseline <migration-id>");
    stdout.WriteLine("  --format <console|github|sarif|json>   (default: console)");
    stdout.WriteLine("  --output <file>              --fail-on <error|warning|none>");
    stdout.WriteLine("  --rules <MIG001,MIG004>      --exclude-rules <MIG007>");
    stdout.WriteLine("  --category <dataloss|failure|locking|hygiene>");
    stdout.WriteLine("  --changed-only --base <git-ref>");
    stdout.WriteLine("  --connection <conn-string>   read-only live row counts to cut false positives");
    stdout.WriteLine("  --small-rows <n>             rows at/below which a table is treated as small");
    stdout.WriteLine("  --deployment-strategy <rolling|bluegreen|maintenance>");
    stdout.WriteLine("  --no-color");
}
