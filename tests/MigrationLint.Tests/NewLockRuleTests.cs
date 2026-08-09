using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

/// <summary>MIG013–MIG016 — the 0.2 lock-family rules.</summary>
public class NewLockRuleTests
{
    [Fact]
    public void Mig013_FiresOnCheckConstraint_Postgres()
    {
        var report = TestHarness.Run("Bad_AddCheckConstraint", Provider.PostgreSql);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG013");
        Assert.Contains("NOT VALID", v.SafeAlternative);
    }

    [Fact]
    public void Mig013_FiresOnCheckConstraint_SqlServer()
    {
        var report = TestHarness.Run("Bad_AddCheckConstraint", Provider.SqlServer);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG013");
        Assert.Contains("WITH NOCHECK", v.SafeAlternative);
    }

    [Fact]
    public void Mig013_DoesNotFireOnMySql()
    {
        var report = TestHarness.Run("Bad_AddCheckConstraint", Provider.MySql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG013");
    }

    [Fact]
    public void Mig014_FiresOnAddPrimaryKeyToExistingTable()
    {
        var report = TestHarness.Run("Bad_AddPrimaryKey", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG014");
    }

    [Fact]
    public void Mig015_FiresOnIntToBigintWidening()
    {
        var report = TestHarness.Run("Bad_WidenIntToBigint", Provider.PostgreSql);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG015");
        Assert.Contains("int → long", v.Message);
    }

    [Fact]
    public void Mig015_DoesNotFireOnNarrowing()
    {
        // Narrowing is MIG005's job, not MIG015's.
        var report = TestHarness.Run("Bad_NarrowType", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG015");
    }

    [Fact]
    public void Mig016_FiresOnVolatileDefault()
    {
        var report = TestHarness.Run("Bad_VolatileDefault", Provider.PostgreSql);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG016");
        Assert.Contains("gen_random_uuid", v.Message);
    }

    [Fact]
    public void Mig016_DoesNotFire_WhenTableIsSmall()
    {
        var report = TestHarness.Run("Bad_VolatileDefault", Provider.PostgreSql,
            rowCounts: new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Orders"] = 100 });
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG016");
    }
}
