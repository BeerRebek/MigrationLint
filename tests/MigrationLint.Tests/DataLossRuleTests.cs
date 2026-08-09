using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

public class DataLossRuleTests
{
    [Fact]
    public void Mig001_FiresOnDropColumn()
    {
        var report = TestHarness.Run("Bad_DropColumn", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG001" && v.Severity == Severity.Error);
    }

    [Fact]
    public void Mig001_DowngradesToWarningOutsideRolling()
    {
        var config = new LintConfig { DeploymentStrategy = DeploymentStrategy.BlueGreen };
        var report = TestHarness.Run("Bad_DropColumn", Provider.PostgreSql, config);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG001");
        Assert.Equal(Severity.Warning, v.Severity);
    }

    [Fact]
    public void Mig002_FiresOnDropTable()
    {
        var report = TestHarness.Run("Bad_DropTable", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG002");
    }

    [Fact]
    public void Mig003_FiresOnRenameColumn()
    {
        var report = TestHarness.Run("Bad_RenameColumn", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG003");
    }

    [Fact]
    public void Mig003_DowngradesToInfoInMaintenanceWindow()
    {
        var config = new LintConfig { DeploymentStrategy = DeploymentStrategy.MaintenanceWindow };
        var report = TestHarness.Run("Bad_RenameColumn", Provider.PostgreSql, config);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG003");
        Assert.Equal(Severity.Info, v.Severity);
    }

    [Fact]
    public void Mig004_FiresOnNotNullNoDefault()
    {
        var report = TestHarness.Run("Bad_AddNotNullNoDefault", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void Mig004_DoesNotFireWithDefault()
    {
        var report = TestHarness.Run("Good_AddNotNullWithDefault", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void Mig004_DoesNotFireOnNewTable()
    {
        var report = TestHarness.Run("Good_AddNotNullToNewTable", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void Mig005_FiresOnLengthNarrowing()
    {
        var report = TestHarness.Run("Bad_NarrowType", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG005");
    }

    [Fact]
    public void Mig005_FiresOnPrecisionNarrowing()
    {
        var report = TestHarness.Run("Bad_NarrowPrecision", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG005");
    }

    [Fact]
    public void Mig006_FiresOnNullableToNotNull()
    {
        var report = TestHarness.Run("Bad_NullableToNotNull", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG006");
    }
}
