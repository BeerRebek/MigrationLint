using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MigrationLint.Core.Model;
using OperationKind = MigrationLint.Core.Model.OperationKind;

namespace MigrationLint.Core.Parsing;

/// <summary>
/// Parses a single EF migration <c>.cs</c> file into a <see cref="MigrationIr"/> using
/// Roslyn syntax parsing only. No compilation, no assembly load. Never throws on
/// unrecognized input — unparseable operations are skipped.
/// </summary>
public static class MigrationFileParser
{
    private static readonly IReadOnlyDictionary<string, OperationKind> MethodMap =
        new Dictionary<string, OperationKind>(StringComparer.Ordinal)
        {
            ["AddColumn"] = OperationKind.AddColumn,
            ["DropColumn"] = OperationKind.DropColumn,
            ["AlterColumn"] = OperationKind.AlterColumn,
            ["RenameColumn"] = OperationKind.RenameColumn,
            ["CreateTable"] = OperationKind.CreateTable,
            ["DropTable"] = OperationKind.DropTable,
            ["RenameTable"] = OperationKind.RenameTable,
            ["CreateIndex"] = OperationKind.CreateIndex,
            ["DropIndex"] = OperationKind.DropIndex,
            ["AddForeignKey"] = OperationKind.AddForeignKey,
            ["AddUniqueConstraint"] = OperationKind.AddUniqueConstraint,
            ["Sql"] = OperationKind.RawSql,
            ["InsertData"] = OperationKind.InsertData,
            ["UpdateData"] = OperationKind.UpdateData,
            ["DeleteData"] = OperationKind.DeleteData,
        };

    /// <summary>Method names seen on migrationBuilder that we do not map — collected for the corpus review.</summary>
    public static bool TryParse(string filePath, string source, out MigrationIr migration, out IReadOnlyList<string> unmappedMethods)
    {
        var unmapped = new HashSet<string>(StringComparer.Ordinal);
        migration = default!;
        unmappedMethods = Array.Empty<string>();

        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root = tree.GetRoot();

        var migrationClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(IsMigrationClass);

        if (migrationClass is null)
        {
            return false;
        }

        var id = Path.GetFileNameWithoutExtension(filePath);
        ParseSuppression(migrationClass, out var justification, out var rules, out var suppressAll, out var invalid);

        var up = ParseMethod(migrationClass, "Up", filePath, tree, unmapped);
        var down = ParseMethod(migrationClass, "Down", filePath, tree, unmapped);

        migration = new MigrationIr
        {
            Id = id,
            FilePath = filePath,
            UpOperations = up,
            DownOperations = down,
            SuppressedRules = rules,
            SuppressionJustification = justification,
            SuppressesAllRules = suppressAll,
            HasSuppressionWithoutJustification = invalid,
        };

        unmappedMethods = unmapped.ToArray();
        return true;
    }

    private static bool IsMigrationClass(ClassDeclarationSyntax cls)
    {
        if (cls.BaseList is null)
        {
            return false;
        }

        foreach (var baseType in cls.BaseList.Types)
        {
            var name = baseType.Type switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                GenericNameSyntax g => g.Identifier.ValueText,
                _ => null,
            };

            if (name == "Migration")
            {
                return true;
            }
        }

        return false;
    }

    private static void ParseSuppression(
        ClassDeclarationSyntax cls,
        out string? justification,
        out IReadOnlyList<string> rules,
        out bool suppressAll,
        out bool invalid)
    {
        justification = null;
        rules = Array.Empty<string>();
        suppressAll = false;
        invalid = false;

        var attribute = cls.AttributeLists
            .SelectMany(l => l.Attributes)
            .FirstOrDefault(a => AttributeName(a) is "SuppressMigrationLint" or "SuppressMigrationLintAttribute");

        if (attribute is null)
        {
            return;
        }

        var args = attribute.ArgumentList?.Arguments;
        if (args is null || args.Value.Count == 0)
        {
            invalid = true;
            return;
        }

        var first = AsStringLiteral(args.Value[0].Expression);
        if (first is null)
        {
            invalid = true;
            return;
        }

        justification = first;
        var ruleIds = new List<string>();
        for (var i = 1; i < args.Value.Count; i++)
        {
            if (AsStringLiteral(args.Value[i].Expression) is { } r)
            {
                ruleIds.Add(r);
            }
        }

        rules = ruleIds;
        suppressAll = ruleIds.Count == 0;
    }

    private static string? AttributeName(AttributeSyntax attribute) =>
        attribute.Name switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null,
        };

    private static IReadOnlyList<MigrationOperationIr> ParseMethod(
        ClassDeclarationSyntax cls,
        string methodName,
        string filePath,
        SyntaxTree tree,
        HashSet<string> unmapped)
    {
        var method = cls.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);

        if (method is null)
        {
            return Array.Empty<MigrationOperationIr>();
        }

        var operations = new List<MigrationOperationIr>();
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member)
            {
                continue;
            }

            // Operation invocations have migrationBuilder as their direct receiver;
            // chained .Annotation(...) calls always wrap outward, so they never sit here.
            if (member.Expression is not IdentifierNameSyntax { Identifier.ValueText: "migrationBuilder" })
            {
                continue;
            }

            var name = member.Name.Identifier.ValueText;
            if (!MethodMap.TryGetValue(name, out var kind))
            {
                if (name != "Annotation")
                {
                    unmapped.Add(name);
                }

                continue;
            }

            operations.Add(BuildOperation(kind, name, invocation, filePath, tree));
        }

        return operations;
    }

    private static MigrationOperationIr BuildOperation(
        OperationKind kind,
        string methodName,
        InvocationExpressionSyntax invocation,
        string filePath,
        SyntaxTree tree)
    {
        var args = ReadArguments.From(invocation, methodName);
        var annotations = CollectAnnotations(invocation);

        var start = tree.GetLineSpan(invocation.Span).StartLinePosition;
        var source = new SourceSpan(filePath, start.Line + 1, start.Character + 1);

        var columns = args.StringList("columns", "column");
        var column = BuildColumn(args, isOld: false);
        var oldColumn = BuildColumn(args, isOld: true);

        return new MigrationOperationIr
        {
            Kind = kind,
            Source = source,
            Table = args.String("table"),
            Name = ResolveName(kind, args),
            Columns = columns,
            Column = column,
            OldColumn = oldColumn,
            IsUnique = args.Bool("unique") ?? false,
            RawSql = kind == OperationKind.RawSql ? args.String("sql") : null,
            Annotations = annotations,
        };
    }

    private static string? ResolveName(OperationKind kind, ReadArguments args) =>
        kind switch
        {
            OperationKind.CreateTable or OperationKind.DropTable => args.String("name") ?? args.String("table"),
            _ => args.String("name"),
        };

    private static ColumnInfo? BuildColumn(ReadArguments args, bool isOld)
    {
        var prefix = isOld ? "old" : string.Empty;

        string P(string name) => isOld ? "old" + char.ToUpperInvariant(name[0]) + name.Substring(1) : name;

        var clr = isOld ? args.String("oldClrType") : args.GenericTypeArgument;
        var store = args.String(P("type"));
        var maxLength = args.Int(P("maxLength"));
        var precision = args.Int(P("precision"));
        var scale = args.Int(P("scale"));
        var nullable = args.Bool(P("nullable"));
        var hasDefault = !isOld && (args.IsPresentAndNotNull("defaultValue") || args.IsPresentAndNotNull("defaultValueSql"));
        var defaultValue = isOld ? null : args.String("defaultValue");

        // Only materialize an old column for AlterColumn, where old* args exist.
        var anySet = clr is not null || store is not null || maxLength is not null ||
                     precision is not null || scale is not null || nullable is not null ||
                     hasDefault || defaultValue is not null;

        if (isOld && !anySet)
        {
            return null;
        }

        _ = prefix;
        return new ColumnInfo
        {
            ClrType = clr,
            StoreType = store,
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale,
            IsNullable = nullable,
            HasDefault = hasDefault,
            DefaultValue = defaultValue,
        };
    }

    private static IReadOnlyDictionary<string, string?> CollectAnnotations(InvocationExpressionSyntax operation)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        // Walk outward: operation -> parent MemberAccess(.Annotation) -> parent Invocation.
        SyntaxNode current = operation;
        while (current.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Annotation" } access
               && access.Parent is InvocationExpressionSyntax annotationCall)
        {
            var callArgs = annotationCall.ArgumentList.Arguments;
            if (callArgs.Count >= 1 && AsStringLiteral(callArgs[0].Expression) is { } key)
            {
                var value = callArgs.Count >= 2 ? LiteralText(callArgs[1].Expression) : null;
                result[key] = value;
            }

            current = annotationCall;
        }

        return result;
    }

    private static string? AsStringLiteral(ExpressionSyntax expr) =>
        expr is LiteralExpressionSyntax { Token.Value: string s } ? s : null;

    private static string? LiteralText(ExpressionSyntax expr) =>
        expr switch
        {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } => "true",
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } => "false",
            LiteralExpressionSyntax { Token.Value: string s } => s,
            LiteralExpressionSyntax lit => lit.Token.ValueText,
            _ => expr.ToString(),
        };
}
