using MigrationLint.Core.Model;
using MigrationLint.Core.Rules;

namespace MigrationLint.Core.Engine;

/// <summary>
/// Runs the rule set over a set of migrations and produces a <see cref="LintReport"/>.
/// Pure over its inputs: discovery/config loading happen before this; formatting after.
/// </summary>
public sealed class RuleEngine
{
    private readonly IReadOnlyList<IMigrationRule> _operationRules;
    private readonly IReadOnlyList<IMigrationLevelRule> _migrationRules;

    public RuleEngine()
        : this(RuleCatalog.OperationRules, RuleCatalog.MigrationRules)
    {
    }

    public RuleEngine(
        IReadOnlyList<IMigrationRule> operationRules,
        IReadOnlyList<IMigrationLevelRule> migrationRules)
    {
        _operationRules = operationRules;
        _migrationRules = migrationRules;
    }

    public LintReport Run(
        IReadOnlyList<MigrationIr> migrations,
        Provider provider,
        LintConfig config,
        int skipped)
    {
        var smallTables = new HashSet<string>(config.Options.SmallTables, StringComparer.OrdinalIgnoreCase);
        var violations = new List<Violation>();

        foreach (var migration in migrations)
        {
            var created = new HashSet<string>(
                migration.UpOperations
                    .Where(o => o.Kind == OperationKind.CreateTable)
                    .Select(o => o.Table ?? o.Name)
                    .Where(t => t is not null)
                    .Select(t => t!),
                StringComparer.OrdinalIgnoreCase);

            var ctx = new LintContext
            {
                MigrationId = migration.Id,
                Provider = provider,
                Strategy = config.DeploymentStrategy,
                AllOperations = migration.UpOperations,
                TablesCreatedInThisMigration = created,
                SmallTables = smallTables,
                Config = config,
            };

            var raw = new List<Violation>();

            foreach (var op in migration.UpOperations)
            {
                foreach (var rule in _operationRules)
                {
                    raw.AddRange(rule.Analyze(op, ctx));
                }
            }

            foreach (var rule in _migrationRules)
            {
                raw.AddRange(rule.Analyze(migration, ctx));
            }

            violations.AddRange(ApplySuppression(raw, migration));
        }

        // Stable ordering: by file, then line, then rule id.
        violations.Sort((a, b) =>
        {
            var f = string.CompareOrdinal(a.Source.FilePath, b.Source.FilePath);
            if (f != 0) return f;
            var l = a.Source.Line.CompareTo(b.Source.Line);
            if (l != 0) return l;
            return string.CompareOrdinal(a.RuleId, b.RuleId);
        });

        return new LintReport(violations, migrations.Count, skipped);
    }

    private static IEnumerable<Violation> ApplySuppression(IEnumerable<Violation> violations, MigrationIr migration)
    {
        foreach (var v in violations)
        {
            // MIG000 reports an invalid suppression and must never itself be suppressed.
            if (v.RuleId == "MIG000")
            {
                yield return v;
                continue;
            }

            if (migration.SuppressesAllRules)
            {
                continue;
            }

            if (migration.SuppressedRules.Contains(v.RuleId, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return v;
        }
    }
}
