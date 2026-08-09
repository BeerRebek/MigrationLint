using MigrationLint.Core.Model;

namespace MigrationLint.Core.Engine;

/// <summary>A single step in a safe rollout, tagged with the phase it belongs to.</summary>
public sealed record PlanStep(int Phase, string Text, string? RuleId);

/// <summary>
/// Reframes a migration as a safe deployment sequence: expand (deploy now) → migrate
/// (backfill/validate) → contract (after the previous deploy is fully rolled out). Pure over the IR
/// plus the rules that flagged each operation, so it stays in step with the rule set.
/// </summary>
public static class DeploymentPlanner
{
    public const int Expand = 1;
    public const int Migrate = 2;
    public const int Contract = 3;

    public static readonly IReadOnlyDictionary<int, string> PhaseLabels = new Dictionary<int, string>
    {
        [Expand] = "expand (safe to deploy now)",
        [Migrate] = "migrate (backfill / validate)",
        [Contract] = "contract (after the previous deploy is fully rolled out)",
    };

    public static IReadOnlyList<PlanStep> Plan(
        MigrationIr migration,
        IReadOnlyDictionary<int, IReadOnlyList<string>> rulesByLine)
    {
        var steps = new List<PlanStep>();
        foreach (var op in migration.UpOperations)
        {
            rulesByLine.TryGetValue(op.Source.Line, out var rules);
            AddSteps(op, rules ?? Array.Empty<string>(), steps);
        }

        return steps;
    }

    private static void AddSteps(MigrationOperationIr op, IReadOnlyList<string> rules, List<PlanStep> steps)
    {
        bool Has(string id) => rules.Contains(id);
        string? Tag(string id) => Has(id) ? id : null;
        var t = op.Table ?? "?";

        switch (op.Kind)
        {
            case OperationKind.AddColumn when Has("MIG004"):
                steps.Add(new PlanStep(Expand, $"Add column {t}.{op.Name} as nullable (no default)", "MIG004"));
                steps.Add(new PlanStep(Migrate, $"Backfill {t}.{op.Name} in batches, then set NOT NULL", "MIG004"));
                break;
            case OperationKind.AddColumn when Has("MIG016"):
                steps.Add(new PlanStep(Expand, $"Add column {t}.{op.Name} as nullable, no default", "MIG016"));
                steps.Add(new PlanStep(Migrate, $"Backfill {t}.{op.Name}, then set the default", "MIG016"));
                break;
            case OperationKind.AddColumn when Has("MIG019"):
                steps.Add(new PlanStep(Migrate, $"Add stored computed column {t}.{op.Name} (rewrites the table)", "MIG019"));
                break;
            case OperationKind.AddColumn:
                steps.Add(new PlanStep(Expand, $"Add column {t}.{op.Name}", null));
                break;

            case OperationKind.AlterColumn when Has("MIG006"):
                steps.Add(new PlanStep(Migrate, $"Backfill NULLs in {t}.{op.Name}, then set NOT NULL", "MIG006"));
                break;
            case OperationKind.AlterColumn when Has("MIG005"):
                steps.Add(new PlanStep(Migrate, $"Migrate {t}.{op.Name} to the narrower type via a new column", "MIG005"));
                break;
            case OperationKind.AlterColumn when Has("MIG015"):
                steps.Add(new PlanStep(Expand, $"Add a wider column beside {t}.{op.Name}", "MIG015"));
                steps.Add(new PlanStep(Migrate, $"Backfill and switch reads to the wider {t}.{op.Name}", "MIG015"));
                steps.Add(new PlanStep(Contract, $"Drop the old {t}.{op.Name} column", "MIG015"));
                break;
            case OperationKind.AlterColumn:
                steps.Add(new PlanStep(Migrate, $"Alter column {t}.{op.Name}", null));
                break;

            case OperationKind.CreateIndex when Has("MIG007"):
                steps.Add(new PlanStep(Expand, $"Create index {op.Name} CONCURRENTLY on {t}", "MIG007"));
                break;
            case OperationKind.CreateIndex when Has("MIG008"):
                steps.Add(new PlanStep(Migrate, $"Resolve duplicates, then add unique index {op.Name} on {t}", "MIG008"));
                break;
            case OperationKind.CreateIndex:
                steps.Add(new PlanStep(Expand, $"Create index {op.Name} on {t}", null));
                break;

            case OperationKind.CreateTable:
                steps.Add(new PlanStep(Expand, $"Create table {op.Name ?? t}", null));
                break;

            case OperationKind.AddForeignKey when Has("MIG009"):
                steps.Add(new PlanStep(Expand, $"Add FK {op.Name} on {t} as NOT VALID (fast)", "MIG009"));
                steps.Add(new PlanStep(Migrate, $"Validate FK {op.Name} (no write lock)", "MIG009"));
                break;
            case OperationKind.AddForeignKey:
                steps.Add(new PlanStep(Migrate, $"Add FK {op.Name} on {t}", null));
                break;

            case OperationKind.AddCheckConstraint when Has("MIG013"):
                steps.Add(new PlanStep(Expand, $"Add check {op.Name} on {t} as NOT VALID (fast)", "MIG013"));
                steps.Add(new PlanStep(Migrate, $"Validate check {op.Name}", "MIG013"));
                break;
            case OperationKind.AddCheckConstraint:
                steps.Add(new PlanStep(Migrate, $"Add check constraint {op.Name} on {t}", null));
                break;

            case OperationKind.AddUniqueConstraint:
                steps.Add(new PlanStep(Migrate, $"Resolve duplicates, then add unique constraint {op.Name} on {t}", Tag("MIG008")));
                break;
            case OperationKind.AddPrimaryKey:
                steps.Add(new PlanStep(Migrate, $"Build a unique index concurrently, then add PK {op.Name} on {t}", Tag("MIG014")));
                break;

            case OperationKind.DropColumn:
                steps.Add(new PlanStep(Contract, $"Drop column {t}.{op.Name}", Tag("MIG001")));
                break;
            case OperationKind.DropTable:
                steps.Add(new PlanStep(Contract, $"Drop table {op.Table ?? op.Name}", Tag("MIG002")));
                break;
            case OperationKind.RenameColumn:
            case OperationKind.RenameTable:
                steps.Add(new PlanStep(Expand, $"Add the new name and dual-write ({op.Target})", Tag("MIG003")));
                steps.Add(new PlanStep(Contract, $"Drop the old name after rollout ({op.Target})", Tag("MIG003")));
                break;
            case OperationKind.DropIndex when Has("MIG020"):
                steps.Add(new PlanStep(Expand, $"Drop index {op.Name} CONCURRENTLY", "MIG020"));
                break;

            case OperationKind.RawSql:
            case OperationKind.InsertData:
            case OperationKind.UpdateData:
            case OperationKind.DeleteData:
                steps.Add(new PlanStep(Migrate, "Data change (run in its own deploy or a background job)",
                    Has("MIG010") ? "MIG010" : Tag("MIG011")));
                break;
        }
    }
}
