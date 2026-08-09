namespace MigrationLint.Core;

/// <summary>
/// Marks a migration as reviewed-and-accepted for specific rules. MigrationLint recognizes
/// this attribute by name during source parsing, so you may either reference
/// <c>MigrationLint.Core</c> or copy this type into your own project.
/// </summary>
/// <example>
/// <code>
/// [SuppressMigrationLint("Orders is a small lookup table; scan is trivial.", "MIG008")]
/// public partial class AddUniqueSku : Migration { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class SuppressMigrationLintAttribute : Attribute
{
    /// <param name="justification">Why the flagged operation is safe. Required — omitting it triggers MIG000.</param>
    /// <param name="ruleIds">Rule ids to suppress. Empty suppresses every rule for the migration.</param>
    public SuppressMigrationLintAttribute(string justification, params string[] ruleIds)
    {
        Justification = justification;
        RuleIds = ruleIds;
    }

    public string Justification { get; }

    public IReadOnlyList<string> RuleIds { get; }
}
