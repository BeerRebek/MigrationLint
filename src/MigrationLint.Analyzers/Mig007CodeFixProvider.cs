using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MigrationLint.Analyzers;

/// <summary>
/// Lightbulb fix for MIG007: append the provider's online-index annotation to the CreateIndex call
/// (<c>Npgsql:CreatedConcurrently</c> on PostgreSQL, <c>SqlServer:Online</c> on SQL Server).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Mig007CodeFixProvider)), Shared]
public sealed class Mig007CodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MIG007");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(IsCreateIndexCall);

        if (invocation is null)
        {
            return;
        }

        // The message distinguishes PostgreSQL (CONCURRENTLY) from SQL Server (ONLINE = ON).
        var isPostgres = diagnostic.GetMessage().Contains("CONCURRENTLY");
        var key = isPostgres ? "Npgsql:CreatedConcurrently" : "SqlServer:Online";
        var title = isPostgres
            ? "Build the index CONCURRENTLY (PostgreSQL)"
            : "Build the index with ONLINE = ON (SQL Server)";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => AddAnnotationAsync(context.Document, root, invocation, key, ct),
                equivalenceKey: "MIG007-" + key),
            diagnostic);
    }

    private static bool IsCreateIndexCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "CreateIndex",
            Expression: IdentifierNameSyntax { Identifier.ValueText: "migrationBuilder" },
        };

    private static Task<Document> AddAnnotationAsync(
        Document document, SyntaxNode root, InvocationExpressionSyntax invocation, string key, CancellationToken ct)
    {
        var annotationCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation.WithoutTrivia(),
                SyntaxFactory.IdentifierName("Annotation")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(key))),
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
            })));

        var newRoot = root.ReplaceNode(invocation, annotationCall.WithTriviaFrom(invocation));
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
