using System.Text.Json;
using MigrationLint.Core.Formatting;
using MigrationLint.Core.Model;
using Xunit;

namespace MigrationLint.Tests;

public class FormatterTests
{
    private static LintReport SampleReport() =>
        TestHarness.Run("Bad_CreateIndexNoConcurrently", Provider.PostgreSql);

    [Fact]
    public void ConsoleFormatterIncludesRuleAndSafeAlternative()
    {
        var output = new ConsoleFormatter(color: false).Format(SampleReport());
        Assert.Contains("MIG007", output);
        Assert.Contains("Safe alternative:", output);
        Assert.Contains("[locking]", output);
    }

    [Fact]
    public void GitHubFormatterEmitsErrorAnnotation()
    {
        var output = new GitHubFormatter().Format(SampleReport());
        Assert.StartsWith("::error ", output);
        Assert.Contains("line=9", output);
        Assert.Contains("title=MIG007", output);
    }

    [Fact]
    public void JsonFormatterProducesValidJson()
    {
        var output = new JsonFormatter().Format(SampleReport());
        using var doc = JsonDocument.Parse(output);
        Assert.Equal(1, doc.RootElement.GetProperty("errorCount").GetInt32());
        var first = doc.RootElement.GetProperty("violations")[0];
        Assert.Equal("MIG007", first.GetProperty("ruleId").GetString());
        Assert.Equal("locking", first.GetProperty("category").GetString());
        Assert.Equal(9, first.GetProperty("line").GetInt32());
    }

    [Fact]
    public void MarkdownFormatterProducesTableWithLinkedRule()
    {
        var output = new MarkdownFormatter().Format(SampleReport());
        Assert.Contains("## MigrationLint", output);
        Assert.Contains("[MIG007](https://github.com/BeerRebek/MigrationLint", output);
        Assert.Contains("| | Rule | Location | Issue |", output);
    }

    [Fact]
    public void SarifFormatterProducesValid_2_1_0_Structure()
    {
        var output = new SarifFormatter().Format(SampleReport());
        using var doc = JsonDocument.Parse(output);

        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
        var run = doc.RootElement.GetProperty("runs")[0];
        Assert.Equal("MigrationLint", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());

        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.True(rules.GetArrayLength() >= 12);

        var result = run.GetProperty("results")[0];
        Assert.Equal("MIG007", result.GetProperty("ruleId").GetString());
        Assert.Equal("error", result.GetProperty("level").GetString());
        var region = result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");
        Assert.Equal(9, region.GetProperty("startLine").GetInt32());
    }
}
