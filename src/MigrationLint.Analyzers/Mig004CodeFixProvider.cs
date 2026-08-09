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
/// Lightbulb fixes for MIG004 (NOT NULL column added without a default):
///   1. Add a type-appropriate <c>defaultValue</c> (single-deployment fix), when the CLR type is known.
///   2. Make the column nullable (step 1 of the backfill-then-tighten approach) — always available.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Mig004CodeFixProvider)), Shared]
public sealed class Mig004CodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MIG004");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var invocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(IsAddColumnCall);

        if (invocation is null)
        {
            return;
        }

        // Fix 1: add a default value, when we can synthesize a literal for the CLR type.
        if (DefaultLiteralFor(ClrType(invocation)) is { } literal)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add a default value",
                    ct => AddDefaultAsync(context.Document, root, invocation, literal, ct),
                    equivalenceKey: "MIG004-default"),
                diagnostic);
        }

        // Fix 2: make the column nullable (backfill, then tighten later).
        var nullableArg = invocation.ArgumentList.Arguments
            .FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == "nullable");
        if (nullableArg is { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } })
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the column nullable (backfill separately)",
                    ct => MakeNullableAsync(context.Document, root, nullableArg, ct),
                    equivalenceKey: "MIG004-nullable"),
                diagnostic);
        }
    }

    private static bool IsAddColumnCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "AddColumn",
            Expression: IdentifierNameSyntax { Identifier.ValueText: "migrationBuilder" },
        };

    private static string? ClrType(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax g }
        && g.TypeArgumentList.Arguments.Count == 1
            ? g.TypeArgumentList.Arguments[0].ToString()
            : null;

    private static ExpressionSyntax? DefaultLiteralFor(string? clrType) =>
        clrType switch
        {
            "string" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("")),
            "bool" => SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),
            "int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort"
                or "decimal" or "double" or "float" =>
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
            _ => null,
        };

    private static Task<Document> AddDefaultAsync(
        Document document, SyntaxNode root, InvocationExpressionSyntax invocation, ExpressionSyntax literal, CancellationToken ct)
    {
        var arg = SyntaxFactory.Argument(literal)
            .WithNameColon(SyntaxFactory.NameColon("defaultValue"));
        var newInvocation = invocation.WithArgumentList(invocation.ArgumentList.AddArguments(arg));
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(invocation, newInvocation)));
    }

    private static Task<Document> MakeNullableAsync(
        Document document, SyntaxNode root, ArgumentSyntax nullableArg, CancellationToken ct)
    {
        var newArg = nullableArg.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(nullableArg, newArg)));
    }
}
