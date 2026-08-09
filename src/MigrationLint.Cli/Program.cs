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

// Top-level help: no args, or a bare help/--help/-h with no command after it.
if (args.Length == 0 ||
    (args[0] is "--help" or "-h" or "help" && args.Length == 1))
{
    Help.General(stdout);
    return 0;
}

// `migrationlint help <command>` → that command's help.
if (args[0] == "help" && args.Length >= 2)
{
    PrintCommandHelp(args[1], stdout);
    return 0;
}

var parsed = ParsedArgs.Parse(args);

// `migrationlint <command> --help|-h` → that command's help.
if (parsed.Command is not null && (parsed.Flag("help") || parsed.Flag("h") || HasShortHelp(args)))
{
    PrintCommandHelp(parsed.Command, stdout);
    return 0;
}

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

static bool HasShortHelp(string[] args) => args.Contains("-h");

static void PrintCommandHelp(string command, TextWriter stdout)
{
    switch (command)
    {
        case "check": Help.Check(stdout); break;
        case "baseline": Help.Baseline(stdout); break;
        case "explain": Help.Explain(stdout); break;
        case "list-rules": ListRulesCommand.Run(stdout); break;
        default: Help.General(stdout); break;
    }
}
