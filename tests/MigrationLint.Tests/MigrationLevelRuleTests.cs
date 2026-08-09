using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

public class MigrationLevelRuleTests
{
    [Fact]
    public void Mig010_FiresWhenSchemaMixedWithData()
    {
        var report = TestHarness.Run("Bad_MixedDdlDml", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG010");
    }

    [Fact]
    public void Mig010_FiresWhenSchemaMixedWithStructuredInsertData()
    {
        const string source = @"
using Microsoft.EntityFrameworkCore.Migrations;
public partial class M : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: ""Region"", table: ""Customers"", nullable: true);
        migrationBuilder.InsertData(table: ""Customers"", column: ""Region"", value: ""unknown"");
    }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}";
        MigrationLint.Core.Parsing.MigrationFileParser.TryParse("M.cs", source, out var m, out _);
        var report = new MigrationLint.Core.Engine.RuleEngine()
            .Run(new[] { m }, Provider.PostgreSql, new LintConfig(), 0);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG010");
    }

    [Fact]
    public void Mig011_FiresOnDestructiveSql()
    {
        var report = TestHarness.Run("Bad_DestructiveSql", Provider.PostgreSql);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG011");
        Assert.Contains("TRUNCATE", v.Message);
    }

    [Fact]
    public void Mig012_FiresWhenTooManyOperations()
    {
        var report = TestHarness.Run("Bad_TooManyOperations", Provider.PostgreSql);
        var v = Assert.Single(report.Violations, x => x.RuleId == "MIG012");
        Assert.Contains("11 operations", v.Message);
    }

    [Fact]
    public void Mig012_RespectsConfiguredThreshold()
    {
        var config = new LintConfig { Options = new LintOptions { MaxOperationsPerMigration = 20 } };
        var report = TestHarness.Run("Bad_TooManyOperations", Provider.PostgreSql, config);
        Assert.DoesNotContain(report.Violations, v => v.RuleId == "MIG012");
    }

    [Fact]
    public void Mig000_FiresOnSuppressionWithoutJustification()
    {
        var report = TestHarness.Run("Edge_SuppressionNoJustification", Provider.PostgreSql);
        Assert.Contains(report.Violations, v => v.RuleId == "MIG000" && v.Severity == Severity.Error);
    }
}
