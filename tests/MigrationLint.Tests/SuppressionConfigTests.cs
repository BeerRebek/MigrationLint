using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;
using Xunit;

namespace MigrationLint.Tests;

public class SuppressionConfigTests
{
    [Fact]
    public void ConfigOffDisablesRule()
    {
        var config = new LintConfig
        {
            Rules = new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase) { ["MIG004"] = Severity.Off },
        };
        var report = TestHarness.Run("Bad_AddNotNullNoDefault", Provider.PostgreSql, config);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG004");
    }

    [Fact]
    public void ConfigOverrideChangesSeverity()
    {
        var config = new LintConfig
        {
            Rules = new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase) { ["MIG004"] = Severity.Warning },
        };
        var report = TestHarness.Run("Bad_AddNotNullNoDefault", Provider.PostgreSql, config);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG004");
        Assert.Equal(Severity.Warning, v.Severity);
    }

    [Fact]
    public void SuppressionAttributeSuppressesNamedRuleOnly()
    {
        var report = TestHarness.Run("Edge_SuppressedMigration", Provider.PostgreSql);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG001");
    }

    [Fact]
    public void SuppressionParsesJustification()
    {
        var m = TestHarness.Parse("Edge_SuppressedMigration");
        Assert.Contains("MIG001", m.SuppressedRules);
        Assert.False(string.IsNullOrEmpty(m.SuppressionJustification));
        Assert.False(m.SuppressesAllRules);
    }

    [Fact]
    public void SuppressionWithoutRulesSuppressesAll()
    {
        // Simulate a whole-migration suppression by parsing a crafted source.
        const string source = @"
using Microsoft.EntityFrameworkCore.Migrations;
[SuppressMigrationLint(""reviewed by DBA"")]
public partial class M : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: ""X"", table: ""Orders"");
    }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}";
        MigrationFileParser.TryParse("M.cs", source, out var m, out _);
        Assert.True(m.SuppressesAllRules);

        var report = new RuleEngine().Run(new[] { m }, Provider.PostgreSql, new LintConfig(), 0);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void BaselineSkipsMigrationsAtOrBeforeIt()
    {
        var files = TestHarness.AllFixtures();
        var baseline = "20260101000015_Bad_NullableToNotNull";
        var (migrations, skipped, _) = MigrationDiscovery.LoadAll(files, baseline);

        Assert.True(skipped >= 15);
        Assert.All(migrations, m => Assert.True(string.CompareOrdinal(m.Id, baseline) > 0));
    }
}
