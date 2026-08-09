using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

/// <summary>Live-DB awareness: row counts (from --connection) suppress false positives.</summary>
public class LiveStatsTests
{
    private static Dictionary<string, long> Rows(string table, long count) =>
        new(StringComparer.OrdinalIgnoreCase) { [table] = count };

    [Fact]
    public void Mig004_DoesNotFire_WhenTableIsEmpty()
    {
        var report = TestHarness.Run("Bad_AddNotNullNoDefault", Provider.PostgreSql, rowCounts: Rows("Orders", 0));
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void Mig004_StillFires_WhenTableHasRows()
    {
        var report = TestHarness.Run("Bad_AddNotNullNoDefault", Provider.PostgreSql, rowCounts: Rows("Orders", 5));
        Assert.Contains(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void Mig006_DoesNotFire_WhenTableIsEmpty()
    {
        var report = TestHarness.Run("Bad_NullableToNotNull", Provider.PostgreSql, rowCounts: Rows("Orders", 0));
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG006");
    }

    [Fact]
    public void Mig007_DoesNotFire_WhenTableIsSmallByRowCount()
    {
        // Default threshold is 10,000; a 500-row table is small enough to build an index inline.
        var report = TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.PostgreSql, rowCounts: Rows("Orders", 500));
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void Mig007_StillFires_WhenTableIsLarge()
    {
        var report = TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.PostgreSql, rowCounts: Rows("Orders", 5_000_000));
        Assert.Contains(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void RowCount_EnrichesTheMessageWithScale()
    {
        var report = TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.PostgreSql, rowCounts: Rows("Orders", 4_238_901));
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG007");
        Assert.Contains("4,238,901 rows", v.Message);
    }

    [Fact]
    public void Mig006_DoesNotFire_WhenColumnHasNoNulls()
    {
        var nulls = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Orders.Status"] = 0 };
        var report = TestHarness.Run("Bad_NullableToNotNull", Provider.PostgreSql, nullCounts: nulls);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG006");
    }

    [Fact]
    public void Mig006_StillFires_WhenColumnHasNulls()
    {
        var nulls = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Orders.Status"] = 42 };
        var report = TestHarness.Run("Bad_NullableToNotNull", Provider.PostgreSql, nullCounts: nulls);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG006");
    }
}
