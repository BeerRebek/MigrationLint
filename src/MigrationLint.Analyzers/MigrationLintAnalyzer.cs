using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Analyzers;

/// <summary>
/// Runs the MigrationLint rule engine as a Roslyn analyzer, so lock/data-loss diagnostics appear
/// inline in the IDE the moment EF generates a migration. Reuses the exact same rules as the CLI
/// (they are pure functions of <c>(operation, context)</c>) — this is what §5.3 of the PRD enables.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MigrationLintAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // Provider is determined once per compilation from the ModelSnapshot's annotations.
            var provider = DetectProvider(start.Compilation);
            start.RegisterSyntaxTreeAction(tree => AnalyzeTree(tree, provider));
        });
    }

    private static Provider DetectProvider(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.FilePath is { } path &&
                path.EndsWith("ModelSnapshot.cs", System.StringComparison.OrdinalIgnoreCase))
            {
                var provider = ProviderDetector.FromAnnotationText(tree.GetText().ToString());
                if (provider != Provider.Unknown)
                {
                    return provider;
                }
            }
        }

        return Provider.Unknown;
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context, Provider provider)
    {
        var path = context.Tree.FilePath ?? string.Empty;
        if (path.EndsWith("ModelSnapshot.cs", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".Designer.cs", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceText = context.Tree.GetText(context.CancellationToken);
        if (!MigrationFileParser.TryParse(path, sourceText.ToString(), out var migration, out _))
        {
            return;
        }

        var report = new RuleEngine().Run(new[] { migration }, provider, LintConfig.Default, skipped: 0);

        foreach (var violation in report.Violations)
        {
            var location = MakeLocation(context.Tree, sourceText, violation.Source);
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.ById[violation.RuleId], location, violation.Message));
        }
    }

    private static Location MakeLocation(SyntaxTree tree, SourceText text, SourceSpan source)
    {
        var lineIndex = source.Line - 1;
        if (lineIndex < 0 || lineIndex >= text.Lines.Count)
        {
            return Location.Create(tree, new TextSpan(0, 0));
        }

        var line = text.Lines[lineIndex];
        var start = System.Math.Min(line.Start + System.Math.Max(0, source.Column - 1), line.End);
        return Location.Create(tree, TextSpan.FromBounds(start, line.End));
    }
}
