using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

public class LockRuleTests
{
    [Fact]
    public void Mig007_FiresOnPostgresWithoutConcurrently()
    {
        var report = TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG007" && v.Severity == Severity.Error);
    }

    [Fact]
    public void Mig007_DoesNotFireWithConcurrentlyAnnotation()
    {
        var report = TestHarness.Run("Good_CreateIndexConcurrently", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void Mig007_FiresOnSqlServerWithoutOnline()
    {
        var report = TestHarness.Run("Bad_CreateIndexSqlServerNoOnline", Provider.SqlServer);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG007");
        Assert.Contains("ONLINE = ON", v.Message);
    }

    [Fact]
    public void Mig007_DoesNotFireOnMySql()
    {
        var report = TestHarness.Run("Good_CreateIndexMySql", Provider.MySql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void Mig007_DoesNotFireOnUnknownProvider()
    {
        var report = TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.Unknown);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void Mig008_FiresOnUniqueIndex()
    {
        var report = TestHarness.Run("Bad_UniqueIndex", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG008");
    }

    [Fact]
    public void Mig009_FiresOnPostgresForeignKey()
    {
        var report = TestHarness.Run("Bad_AddForeignKey", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG009" && v.Severity == Severity.Warning);
    }

    [Fact]
    public void Mig009_FiresOnSqlServerWithNoCheckGuidance()
    {
        var report = TestHarness.Run("Bad_AddForeignKey", Provider.SqlServer);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG009");
        Assert.Contains("WITH NOCHECK", v.SafeAlternative);
    }

    [Fact]
    public void Mig009_DoesNotFireOnMySql()
    {
        var report = TestHarness.Run("Bad_AddForeignKey", Provider.MySql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG009");
    }
}
