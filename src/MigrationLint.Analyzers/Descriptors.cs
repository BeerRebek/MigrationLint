using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using MigrationLint.Core.Rules;
using CoreSeverity = MigrationLint.Core.Model.Severity;

namespace MigrationLint.Analyzers;

/// <summary>Builds one <see cref="DiagnosticDescriptor"/> per MigrationLint rule from the catalog.</summary>
internal static class Descriptors
{
    public static readonly ImmutableArray<DiagnosticDescriptor> All;
    public static readonly ImmutableDictionary<string, DiagnosticDescriptor> ById;

    static Descriptors()
    {
        var builder = ImmutableArray.CreateBuilder<DiagnosticDescriptor>();
        foreach (var rule in RuleCatalog.All)
        {
            builder.Add(new DiagnosticDescriptor(
                id: rule.Id,
                title: rule.Title,
                messageFormat: "{0}",
                category: "MigrationLint." + rule.Category,
                defaultSeverity: Map(rule.DefaultSeverity),
                isEnabledByDefault: rule.DefaultSeverity != CoreSeverity.Off,
                description: rule.Title,
                helpLinkUri: $"https://github.com/BeerRebek/MigrationLint/blob/main/docs/rules/{rule.Id}.md"));
        }

        All = builder.ToImmutable();
        ById = All.ToImmutableDictionary(d => d.Id);
    }

    private static DiagnosticSeverity Map(CoreSeverity severity) =>
        severity switch
        {
            CoreSeverity.Error => DiagnosticSeverity.Error,
            CoreSeverity.Warning => DiagnosticSeverity.Warning,
            CoreSeverity.Info => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Hidden,
        };
}
