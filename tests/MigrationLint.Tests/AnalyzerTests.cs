using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MigrationLint.Analyzers;
using Xunit;

namespace MigrationLint.Tests;

public class AnalyzerTests
{
    // A minimal ModelSnapshot so the analyzer detects the PostgreSQL provider.
    private const string PostgresSnapshot =
        "public class AppDbContextModelSnapshot { void B() { X(\"Npgsql:ValueGenerationStrategy\"); } }";

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string migrationPath)
    {
        var migrationSource = File.ReadAllText(TestHarness.FixturePath(migrationPath));
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(PostgresSnapshot, path: "AppDbContextModelSnapshot.cs"),
            CSharpSyntaxTree.ParseText(migrationSource, path: migrationPath + ".cs"),
        };

        var compilation = CSharpCompilation.Create(
            "AnalyzerTest", trees,
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new MigrationLintAnalyzer()));

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>Runs a code-fix provider on the fixture and returns the resulting document text for the chosen action.</summary>
    private static async Task<string> ApplyFix(
        string fixturePath, string ruleId, CodeFixProvider provider, string? equivalenceKey = null)
    {
        var source = File.ReadAllText(TestHarness.FixturePath(fixturePath));
        var diagnostic = Assert.Single(await Analyze(fixturePath), d => d.Id == ruleId);

        using var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument(fixturePath + ".cs", source, filePath: fixturePath + ".cs");

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (a, _) => actions.Add(a), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context);

        var action = equivalenceKey is null
            ? actions.Single()
            : actions.Single(a => a.EquivalenceKey == equivalenceKey);

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applied = ((ApplyChangesOperation)operations.Single()).ChangedSolution;
        return (await applied.GetDocument(document.Id)!.GetTextAsync()).ToString();
    }

    [Fact]
    public async Task Analyzer_ReportsMig007_ReusingTheRuleEngine()
    {
        var diagnostics = await Analyze("Bad_CreateIndexNoConcurrently");
        Assert.Contains(diagnostics, d => d.Id == "MIG007" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyzer_DoesNotReportWhenAnnotationPresent()
    {
        var diagnostics = await Analyze("Good_CreateIndexConcurrently");
        Assert.DoesNotContain(diagnostics, d => d.Id == "MIG007");
    }

    [Fact]
    public async Task Mig007CodeFix_AppendsConcurrentlyAnnotation()
    {
        var text = await ApplyFix("Bad_CreateIndexNoConcurrently", "MIG007", new Mig007CodeFixProvider());
        Assert.Contains("Npgsql:CreatedConcurrently", text);
    }

    [Fact]
    public async Task Mig004CodeFix_AddsDefaultValue()
    {
        var text = await ApplyFix("Bad_AddNotNullNoDefault", "MIG004", new Mig004CodeFixProvider(), "MIG004-default");
        Assert.Contains("defaultValue:", text);
    }

    [Fact]
    public async Task Mig004CodeFix_MakesColumnNullable()
    {
        var text = await ApplyFix("Bad_AddNotNullNoDefault", "MIG004", new Mig004CodeFixProvider(), "MIG004-nullable");
        Assert.Contains("nullable: true", text);
    }

    [Fact]
    public async Task SuppressCodeFix_AddsAttributeWithRuleId()
    {
        var text = await ApplyFix("Bad_CreateIndexNoConcurrently", "MIG007", new SuppressCodeFixProvider());
        Assert.Contains("SuppressMigrationLint", text);
        Assert.Contains("MIG007", text);
    }

    [Fact]
    public async Task Mig009CodeFix_SplitsForeignKeyIntoNotValid()
    {
        var text = await ApplyFix("Bad_AddForeignKey", "MIG009", new FkCheckConstraintCodeFixProvider());
        Assert.Contains("NOT VALID", text);
        Assert.Contains("VALIDATE CONSTRAINT", text);
        Assert.Contains("migrationBuilder.Sql(", text);
    }

    [Fact]
    public async Task Mig013CodeFix_SplitsCheckConstraintIntoNotValid()
    {
        var text = await ApplyFix("Bad_AddCheckConstraint", "MIG013", new FkCheckConstraintCodeFixProvider());
        Assert.Contains("NOT VALID", text);
        Assert.Contains("CHECK (", text);
    }
}
