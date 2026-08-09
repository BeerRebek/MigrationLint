using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;
using Xunit;

namespace MigrationLint.Tests;

/// <summary>MIG017 (SuppressTransaction), MIG018 (snapshot drift), MIG019/020 (rewrite/drop-index).</summary>
public class EfSmartRuleTests
{
    [Fact]
    public void Mig017_FiresWhenConcurrentlyWithoutSuppressTransaction()
    {
        var report = TestHarness.Run("Bad_ConcurrentNoSuppress", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG017" && v.Severity == Severity.Error);
        // MIG007 must stay silent — the CONCURRENTLY annotation is present.
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG007");
    }

    [Fact]
    public void Mig017_DoesNotFireWhenSuppressTransactionPresent()
    {
        var report = TestHarness.Run("Good_CreateIndexConcurrently", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG017");
    }

    [Fact]
    public void Mig019_FiresOnStoredComputedColumn()
    {
        var report = TestHarness.Run("Bad_StoredComputedColumn", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG019");
    }

    [Fact]
    public void Mig020_FiresOnPostgres_NotSqlServer()
    {
        Assert.Contains(TestHarness.Run("Bad_DropIndexNoConcurrently", Provider.PostgreSql).Violations,
            v => v.RuleId == "MIG020");
        Assert.DoesNotContain(TestHarness.Run("Bad_DropIndexNoConcurrently", Provider.SqlServer).Violations,
            v => v.RuleId == "MIG020");
    }

    private const string Snapshot = @"
public class Snap {
    protected void BuildModel(object modelBuilder) {
        modelBuilder.Entity(""Order"").Property(""Id"");
    }
}";

    [Fact]
    public void SnapshotDrift_NoDrift_WhenModelsMatch()
    {
        var designer = @"
public class M {
    protected void BuildTargetModel(object modelBuilder) {
        modelBuilder.Entity(""Order"").Property(""Id"");
    }
}";
        Assert.False(SnapshotDrift.HasDrift(Snapshot, designer));
    }

    [Fact]
    public void SnapshotDrift_Drift_WhenTargetModelHasExtra()
    {
        var designer = @"
public class M {
    protected void BuildTargetModel(object modelBuilder) {
        modelBuilder.Entity(""Order"").Property(""Id"");
        modelBuilder.Entity(""Order"").Property(""Notes"");
    }
}";
        Assert.True(SnapshotDrift.HasDrift(Snapshot, designer));
    }
}
