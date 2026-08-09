using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using MigrationLint.Core.Parsing;

namespace MigrationLint.Analyzers;

/// <summary>
/// Code fix for MIG009 (foreign key) and MIG013 (check constraint): replace the single
/// <c>AddForeignKey</c>/<c>AddCheckConstraint</c> call with the two-step deferred-validation form
/// — <c>NOT VALID</c> + <c>VALIDATE</c> on PostgreSQL, <c>WITH NOCHECK</c> + <c>WITH CHECK CHECK</c>
/// on SQL Server. The validate step no longer blocks writes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FkCheckConstraintCodeFixProvider)), Shared]
public sealed class FkCheckConstraintCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MIG009", "MIG013");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var method = diagnostic.Id == "MIG009" ? "AddForeignKey" : "AddCheckConstraint";

        var invocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => IsBuilderCall(i, method));

        var statement = invocation?.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (invocation is null || statement is null)
        {
            return;
        }

        var isPostgres = diagnostic.GetMessage().Contains("PostgreSQL");
        var (sql1, sql2) = diagnostic.Id == "MIG009"
            ? ForeignKeySql(invocation, isPostgres)
            : CheckConstraintSql(invocation, isPostgres);

        var title = isPostgres
            ? "Split into NOT VALID + VALIDATE (PostgreSQL)"
            : "Split into WITH NOCHECK + WITH CHECK (SQL Server)";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => SplitAsync(context.Document, root, statement, sql1, sql2, ct),
                equivalenceKey: diagnostic.Id + (isPostgres ? "-pg" : "-ss")),
            diagnostic);
    }

    private static bool IsBuilderCall(InvocationExpressionSyntax invocation, string method) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "migrationBuilder" },
        } m && m.Name.Identifier.ValueText == method;

    private static (string, string) ForeignKeySql(InvocationExpressionSyntax invocation, bool pg)
    {
        var args = ReadArguments.From(invocation, "AddForeignKey");
        var name = args.String("name") ?? "fk";
        var table = args.String("table") ?? "table";
        var principal = args.String("principalTable") ?? "principal";
        var cols = Cols(args.StringList("columns", "column"), pg);
        var pcols = Cols(args.StringList("principalColumns", "principalColumn"), pg);

        return pg
            ? ($"ALTER TABLE \"{table}\" ADD CONSTRAINT \"{name}\" FOREIGN KEY ({cols}) REFERENCES \"{principal}\" ({pcols}) NOT VALID;",
               $"ALTER TABLE \"{table}\" VALIDATE CONSTRAINT \"{name}\";")
            : ($"ALTER TABLE [{table}] WITH NOCHECK ADD CONSTRAINT [{name}] FOREIGN KEY ({cols}) REFERENCES [{principal}] ({pcols});",
               $"ALTER TABLE [{table}] WITH CHECK CHECK CONSTRAINT [{name}];");
    }

    private static (string, string) CheckConstraintSql(InvocationExpressionSyntax invocation, bool pg)
    {
        var args = ReadArguments.From(invocation, "AddCheckConstraint");
        var name = args.String("name") ?? "ck";
        var table = args.String("table") ?? "table";
        var check = args.String("sql") ?? "/* condition */";

        return pg
            ? ($"ALTER TABLE \"{table}\" ADD CONSTRAINT \"{name}\" CHECK ({check}) NOT VALID;",
               $"ALTER TABLE \"{table}\" VALIDATE CONSTRAINT \"{name}\";")
            : ($"ALTER TABLE [{table}] WITH NOCHECK ADD CONSTRAINT [{name}] CHECK ({check});",
               $"ALTER TABLE [{table}] WITH CHECK CHECK CONSTRAINT [{name}];");
    }

    private static string Cols(IReadOnlyList<string> columns, bool pg)
    {
        if (columns.Count == 0)
        {
            return "...";
        }

        return string.Join(", ", columns.Select(c => pg ? $"\"{c}\"" : $"[{c}]"));
    }

    private static Task<Document> SplitAsync(
        Document document, SyntaxNode root, ExpressionStatementSyntax statement, string sql1, string sql2, CancellationToken ct)
    {
        var leading = statement.GetLeadingTrivia();
        var s1 = SqlStatement(sql1).WithLeadingTrivia(leading).WithAdditionalAnnotations(Formatter.Annotation);
        var s2 = SqlStatement(sql2).WithLeadingTrivia(leading)
            .WithTrailingTrivia(statement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(statement, new SyntaxNode[] { s1, s2 });
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static ExpressionStatementSyntax SqlStatement(string sql) =>
        SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("migrationBuilder"),
                    SyntaxFactory.IdentifierName("Sql")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(sql)))))));
}
