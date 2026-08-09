using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

public class PlannerTests
{
    private static IReadOnlyDictionary<int, IReadOnlyList<string>> Rules(int line, params string[] ids) =>
        new Dictionary<int, IReadOnlyList<string>> { [line] = ids };

    [Fact]
    public void Plan_Mig004_SplitsIntoExpandThenMigrate()
    {
        var m = TestHarness.Parse("Bad_AddNotNullNoDefault");
        var op = m.UpOperations.Single(o => o.Kind == OperationKind.AddColumn);

        var steps = DeploymentPlanner.Plan(m, Rules(op.Source.Line, "MIG004"));

        Assert.Contains(steps, s => s.Phase == DeploymentPlanner.Expand);
        Assert.Contains(steps, s => s.Phase == DeploymentPlanner.Migrate && s.RuleId == "MIG004");
    }

    [Fact]
    public void Plan_DropColumn_IsAContractStep()
    {
        var m = TestHarness.Parse("Bad_DropColumn");
        var op = m.UpOperations.Single(o => o.Kind == OperationKind.DropColumn);

        var steps = DeploymentPlanner.Plan(m, Rules(op.Source.Line, "MIG001"));

        Assert.Contains(steps, s => s.Phase == DeploymentPlanner.Contract && s.RuleId == "MIG001");
    }

    [Fact]
    public void Plan_SafeMigration_StaysSinglePhase()
    {
        var m = TestHarness.Parse("Good_CreateIndexConcurrently");
        var steps = DeploymentPlanner.Plan(m, new Dictionary<int, IReadOnlyList<string>>());

        Assert.All(steps, s => Assert.Equal(DeploymentPlanner.Expand, s.Phase));
    }
}
