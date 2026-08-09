namespace MigrationLint.Cli;

/// <summary>
/// Hand-rolled argument parser. Deliberately no System.CommandLine dependency (still preview,
/// churny) — a small parser keeps this a zero-friction tool (PRD §10).
/// </summary>
public sealed class ParsedArgs
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.Ordinal)
    {
        "no-color", "changed-only",
    };

    public string? Command { get; private set; }
    public IReadOnlyList<string> Positionals => _positionals;
    private readonly List<string> _positionals = new();
    private readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public static ParsedArgs Parse(string[] args)
    {
        var result = new ParsedArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg.Substring(2);
                if (BooleanFlags.Contains(name))
                {
                    result._flags.Add(name);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    result._options[name] = args[++i];
                }
                else
                {
                    // Unknown value-less option: record as a set flag.
                    result._flags.Add(name);
                }
            }
            else if (result.Command is null)
            {
                result.Command = arg;
            }
            else
            {
                result._positionals.Add(arg);
            }
        }

        return result;
    }

    public string? Option(string name) => _options.TryGetValue(name, out var v) ? v : null;

    public bool Flag(string name) => _flags.Contains(name);

    public string? FirstPositional => _positionals.Count > 0 ? _positionals[0] : null;
}
