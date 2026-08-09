using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using MigrationLint.Core.Rules;

namespace MigrationLint.Analyzers;

/// <summary>
/// Universal lightbulb: mark the migration as reviewed with
/// <c>[SuppressMigrationLint("&lt;justification&gt;", "MIGxxx")]</c>. Available on every rule
/// except MIG000 (which reports a suppression that lacks a justification).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuppressCodeFixProvider)), Shared]
public sealed class SuppressCodeFixProvider : CodeFixProvider
{
    private const string AttributeName = "SuppressMigrationLint";
    private const string JustificationPlaceholder = "TODO: explain why this migration is safe";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        RuleCatalog.All.Select(r => r.Id).Where(id => id != "MIG000").ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var classDecl = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Suppress {diagnostic.Id} for this migration",
                ct => AddSuppressionAsync(context.Document, root, classDecl, diagnostic.Id, ct),
                equivalenceKey: "suppress-" + diagnostic.Id),
            diagnostic);
    }

    private static Task<Document> AddSuppressionAsync(
        Document document, SyntaxNode root, ClassDeclarationSyntax classDecl, string ruleId, CancellationToken ct)
    {
        var existing = classDecl.AttributeLists
            .SelectMany(l => l.Attributes)
            .FirstOrDefault(a => NameOf(a) == AttributeName);

        SyntaxNode newRoot;
        if (existing is { ArgumentList: { } argList })
        {
            // Append the rule id to the existing attribute unless it is already covered.
            var alreadyCovered = argList.Arguments
                .Skip(1) // first argument is the justification
                .Any(a => a.Expression is LiteralExpressionSyntax { Token.ValueText: var s } && s == ruleId);

            if (alreadyCovered)
            {
                return Task.FromResult(document);
            }

            var updated = existing.WithArgumentList(argList.AddArguments(StringArgument(ruleId)));
            newRoot = root.ReplaceNode(existing, updated);
        }
        else
        {
            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName(AttributeName),
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    StringArgument(JustificationPlaceholder),
                    StringArgument(ruleId),
                })));

            var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithAdditionalAnnotations(Formatter.Annotation);

            newRoot = root.ReplaceNode(classDecl, classDecl.AddAttributeLists(list));
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static AttributeArgumentSyntax StringArgument(string value) =>
        SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value)));

    private static string? NameOf(AttributeSyntax attribute) =>
        attribute.Name switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null,
        };
}
