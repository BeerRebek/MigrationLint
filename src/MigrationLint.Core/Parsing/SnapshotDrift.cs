using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MigrationLint.Core.Parsing;

/// <summary>
/// Detects the "migration committed without a ModelSnapshot update" merge bug (MIG018). EF keeps the
/// newest migration's <c>BuildTargetModel</c> (in its <c>.Designer.cs</c>) identical to the
/// <c>ModelSnapshot</c>'s <c>BuildModel</c>. When a merge leaves the snapshot stale, they diverge —
/// which means the *next* generated migration will be wrong. Pure text comparison, no I/O.
/// </summary>
public static class SnapshotDrift
{
    private static readonly Regex ProductVersion =
        new(@"\.HasAnnotation\(\s*""ProductVersion""[^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// True when the snapshot's model and the newest migration's target model differ. Returns false
    /// (no drift) when either method can't be found, so a missing/odd file never yields a false positive.
    /// </summary>
    public static bool HasDrift(string snapshotSource, string newestDesignerSource)
    {
        var snapshotModel = ExtractMethodBody(snapshotSource, "BuildModel");
        var targetModel = ExtractMethodBody(newestDesignerSource, "BuildTargetModel");

        if (snapshotModel is null || targetModel is null)
        {
            return false;
        }

        return Normalize(snapshotModel) != Normalize(targetModel);
    }

    private static string? ExtractMethodBody(string source, string methodName)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);

        return method?.Body?.ToFullString() ?? method?.ExpressionBody?.ToFullString();
    }

    private static string Normalize(string body)
    {
        // ProductVersion can legitimately differ; everything else must match.
        body = ProductVersion.Replace(body, string.Empty);
        return Whitespace.Replace(body, string.Empty);
    }
}
