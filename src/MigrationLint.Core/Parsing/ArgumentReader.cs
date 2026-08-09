using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MigrationLint.Core.Parsing;

/// <summary>
/// Reads arguments from a <c>migrationBuilder.*</c> invocation into a canonical,
/// name-keyed bag. Handles named arguments (EF's default), positional arguments
/// (via per-method maps), literals, and the generic type argument. Never throws on
/// non-literal expressions — those are recorded as "present but unknown".
/// </summary>
public sealed class ReadArguments
{
    // Positional parameter maps for the methods where positional usage is realistic.
    // Only the parameters MigrationLint reads are mapped; extra positions are ignored.
    private static readonly IReadOnlyDictionary<string, string[]> PositionalMaps =
        new Dictionary<string, string[]>
        {
            ["Sql"] = new[] { "sql" },
            ["DropColumn"] = new[] { "name", "table" },
            ["DropTable"] = new[] { "name" },
            ["DropIndex"] = new[] { "name", "table" },
            ["RenameColumn"] = new[] { "name", "table", "newName" },
            ["RenameTable"] = new[] { "name", "schema", "newName" },
            ["CreateIndex"] = new[] { "name", "table", "column" },
            ["AddColumn"] = new[] { "name", "table" },
            ["AlterColumn"] = new[] { "name", "table" },
            ["AddForeignKey"] = new[] { "name", "table", "column" },
            ["AddUniqueConstraint"] = new[] { "name", "table", "column" },
            ["AddPrimaryKey"] = new[] { "name", "table", "column" },
            ["AddCheckConstraint"] = new[] { "name", "table", "sql" },
        };

    private readonly Dictionary<string, ExpressionSyntax> _byName =
        new(StringComparer.Ordinal);

    public string? GenericTypeArgument { get; private init; }

    public static ReadArguments From(InvocationExpressionSyntax invocation, string methodName)
    {
        var reader = new ReadArguments
        {
            GenericTypeArgument = ExtractGenericArgument(invocation),
        };

        PositionalMaps.TryGetValue(methodName, out var positional);

        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var arg = arguments[i];
            string? key = arg.NameColon?.Name.Identifier.ValueText;
            if (key is null && positional is not null && i < positional.Length)
            {
                key = positional[i];
            }

            if (key is not null)
            {
                reader._byName[key] = arg.Expression;
            }
        }

        return reader;
    }

    private static string? ExtractGenericArgument(InvocationExpressionSyntax invocation)
    {
        // migrationBuilder.AddColumn<string>(...) — the generic name is on the member access.
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name,
            _ => null,
        };

        if (name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count == 1)
        {
            return generic.TypeArgumentList.Arguments[0].ToString();
        }

        return null;
    }

    public bool Has(string name) => _byName.ContainsKey(name);

    public string? String(string name)
    {
        if (!_byName.TryGetValue(name, out var expr))
        {
            return null;
        }

        return AsString(expr);
    }

    /// <summary>Reads a CLR type name from either <c>typeof(int)</c> or a string literal.</summary>
    public string? TypeName(string name)
    {
        if (!_byName.TryGetValue(name, out var expr))
        {
            return null;
        }

        return expr is TypeOfExpressionSyntax typeOf ? typeOf.Type.ToString() : AsString(expr);
    }

    public bool? Bool(string name)
    {
        if (!_byName.TryGetValue(name, out var expr))
        {
            return null;
        }

        return expr switch
        {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } => true,
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } => false,
            _ => null,
        };
    }

    public int? Int(string name)
    {
        if (!_byName.TryGetValue(name, out var expr))
        {
            return null;
        }

        if (expr is LiteralExpressionSyntax { Token.Value: int i })
        {
            return i;
        }

        return null;
    }

    /// <summary>True only when the argument is explicitly present and is not the null literal.</summary>
    public bool IsPresentAndNotNull(string name)
    {
        if (!_byName.TryGetValue(name, out var expr))
        {
            return false;
        }

        return expr is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }

    /// <summary>
    /// Reads <c>columns: new[] { "A", "B" }</c> or <c>column: "A"</c> into a list.
    /// Non-literal / unknown array elements are skipped rather than throwing.
    /// </summary>
    public IReadOnlyList<string> StringList(params string[] names)
    {
        foreach (var name in names)
        {
            if (!_byName.TryGetValue(name, out var expr))
            {
                continue;
            }

            switch (expr)
            {
                case LiteralExpressionSyntax when AsString(expr) is { } single:
                    return new[] { single };

                case ImplicitArrayCreationExpressionSyntax { Initializer: { } init }:
                    return CollectStrings(init);

                case ArrayCreationExpressionSyntax { Initializer: { } init }:
                    return CollectStrings(init);

                case InitializerExpressionSyntax init:
                    return CollectStrings(init);
            }
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> CollectStrings(InitializerExpressionSyntax initializer)
    {
        var result = new List<string>();
        foreach (var element in initializer.Expressions)
        {
            if (AsString(element) is { } s)
            {
                result.Add(s);
            }
        }

        return result;
    }

    private static string? AsString(ExpressionSyntax expr) =>
        expr is LiteralExpressionSyntax { Token.Value: string s } ? s : null;
}
