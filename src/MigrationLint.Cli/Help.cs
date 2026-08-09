namespace MigrationLint.Cli;

/// <summary>All CLI help text: a rich top-level overview plus per-command detail.</summary>
public static class Help
{
    public static void General(TextWriter o)
    {
        o.WriteLine("MigrationLint — the EF Core migration linter that understands database locks.");
        o.WriteLine();
        o.WriteLine("It reads your EF Core migration .cs files (no build, no database, no DbContext");
        o.WriteLine("loading) and flags changes that lock tables or destroy data in production.");
        o.WriteLine();
        o.WriteLine("USAGE");
        o.WriteLine("  migrationlint <command> [options]");
        o.WriteLine();
        o.WriteLine("COMMANDS");
        o.WriteLine("  check [path]        Lint migrations under [path] (default: current directory).");
        o.WriteLine("  init [path]         Scaffold a starter migrationlint.json (detects your provider).");
        o.WriteLine("  baseline [path]     Suggest a baseline id so an existing repo starts clean.");
        o.WriteLine("  explain <MIG007>    Show a rule's category, severity, and docs link.");
        o.WriteLine("  list-rules          List every rule with its default severity and category.");
        o.WriteLine();
        o.WriteLine("WHAT IT DETECTS");
        o.WriteLine("  locking   Index without CONCURRENTLY/ONLINE, unique-constraint scans,");
        o.WriteLine("            foreign keys without deferred validation, DDL mixed with data.");
        o.WriteLine("  dataloss  Dropped columns/tables, renames, type narrowing, destructive SQL.");
        o.WriteLine("  failure   NOT NULL column with no default, nullable -> NOT NULL on live data.");
        o.WriteLine("  hygiene   Oversized migrations, suppressions without a justification.");
        o.WriteLine();
        o.WriteLine("EXAMPLES");
        o.WriteLine("  migrationlint check ./src/Orders.Api");
        o.WriteLine("  migrationlint check --category locking          # try the differentiators first");
        o.WriteLine("  migrationlint check --changed-only --base main  # only this PR's migrations");
        o.WriteLine("  migrationlint check --format sarif -o out.sarif # for CI code scanning");
        o.WriteLine("  migrationlint check --connection \"$CONN\"        # cut false positives with live stats");
        o.WriteLine("  migrationlint explain MIG007");
        o.WriteLine();
        o.WriteLine("GLOBAL");
        o.WriteLine("  --version           Print the tool version.");
        o.WriteLine("  -h, --help          Show this help. Add to a command for its options:");
        o.WriteLine("                      e.g. 'migrationlint check --help'.");
        o.WriteLine();
        o.WriteLine("Docs: https://github.com/BeerRebek/MigrationLint");
    }

    public static void Check(TextWriter o)
    {
        o.WriteLine("migrationlint check [path] [options]");
        o.WriteLine();
        o.WriteLine("Lint EF Core migrations for lock/downtime and data-loss safety. [path] defaults to");
        o.WriteLine("the current directory; the provider and migrations folder are auto-detected.");
        o.WriteLine();
        o.WriteLine("SELECTION");
        o.WriteLine("  --provider <postgres|sqlserver|mysql|sqlite>   Override provider auto-detection.");
        o.WriteLine("  --config <file>                                Path to migrationlint.json.");
        o.WriteLine("  --baseline <migration-id>                      Skip migrations at or before this id.");
        o.WriteLine("  --changed-only --base <git-ref>                Only migrations added in this PR.");
        o.WriteLine("  --rules <MIG001,MIG004>                        Report only these rules.");
        o.WriteLine("  --exclude-rules <MIG007,MIG012>                Report everything except these.");
        o.WriteLine("  --category <dataloss|failure|locking|hygiene>  Report only this category.");
        o.WriteLine();
        o.WriteLine("OUTPUT");
        o.WriteLine("  --format <console|github|sarif|json|markdown>  Default: console.");
        o.WriteLine("  --output <file>                                Write to a file instead of stdout.");
        o.WriteLine("  --fail-on <error|warning|none>                 Severity that exits non-zero (error).");
        o.WriteLine("  --no-color                                     Disable ANSI color.");
        o.WriteLine();
        o.WriteLine("BEHAVIOR");
        o.WriteLine("  --deployment-strategy <rolling|bluegreen|maintenance>   Adjusts which rules fire.");
        o.WriteLine("  --connection <conn-string>                     Read-only live row counts (Postgres/");
        o.WriteLine("                                                 SQL Server) to cut false positives.");
        o.WriteLine("  --small-rows <n>                               Rows at/below which a table is 'small'.");
        o.WriteLine();
        o.WriteLine("EXIT CODES");
        o.WriteLine("  0  clean        1  violations at/above --fail-on");
        o.WriteLine("  2  config error 3  no migration files found");
    }

    public static void Baseline(TextWriter o)
    {
        o.WriteLine("migrationlint baseline [path]");
        o.WriteLine();
        o.WriteLine("Prints the newest migration id and a migrationlint.json snippet. Set it as the");
        o.WriteLine("baseline so a mature repo only lints migrations added from now on.");
    }

    public static void Explain(TextWriter o)
    {
        o.WriteLine("migrationlint explain <MIG007>");
        o.WriteLine();
        o.WriteLine("Shows a rule's title, category, default severity, and a link to its docs page.");
        o.WriteLine("Run 'migrationlint list-rules' to see all rule ids.");
    }
}
