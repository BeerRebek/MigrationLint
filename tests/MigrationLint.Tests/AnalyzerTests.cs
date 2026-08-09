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
            "AnalyzerTest",
            trees,
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new MigrationLintAnalyzer()));

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
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
    public async Task CodeFix_AppendsConcurrentlyAnnotation()
    {
        var migrationPath = "Bad_CreateIndexNoConcurrently.cs";
        var source = File.ReadAllText(TestHarness.FixturePath("Bad_CreateIndexNoConcurrently"));
        var diagnostic = Assert.Single(await Analyze("Bad_CreateIndexNoConcurrently"), d => d.Id == "MIG007");

        using var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument(migrationPath, source, filePath: migrationPath);

        CodeAction? registered = null;
        var context = new CodeFixContext(
            document, diagnostic,
            (action, _) => registered = action,
            CancellationToken.None);

        await new Mig007CodeFixProvider().RegisterCodeFixesAsync(context);
        Assert.NotNull(registered);

        var operations = await registered!.GetOperationsAsync(CancellationToken.None);
        var applied = ((ApplyChangesOperation)operations.Single()).ChangedSolution;
        var newText = (await applied.GetDocument(document.Id)!.GetTextAsync()).ToString();

        Assert.Contains("Npgsql:CreatedConcurrently", newText);
    }
}
