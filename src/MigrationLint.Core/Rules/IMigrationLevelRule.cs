using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// A rule that analyzes a whole migration at once, not a single operation. Used by rules
/// whose condition spans operations (MIG010, MIG012) or inspects the migration itself (MIG000).
/// The universal new-table guard does not apply to these.
/// </summary>
public interface IMigrationLevelRule
{
    string Id { get; }
    string Title { get; }
    Severity DefaultSeverity { get; }
    RuleCategory Category { get; }
    IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx);
}

public abstract class MigrationLevelRuleBase : IMigrationLevelRule
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract Severity DefaultSeverity { get; }
    public abstract RuleCategory Category { get; }

    public abstract IEnumerable<Violation> Analyze(MigrationIr migration, LintContext ctx);

    protected static readonly IEnumerable<Violation> None = Array.Empty<Violation>();

    protected IEnumerable<Violation> One(
        MigrationIr migration,
        LintContext ctx,
        SourceSpan source,
        string target,
        string message,
        string safeAlternative,
        Severity computedDefault)
    {
        var severity = ctx.SeverityFor(Id, computedDefault);
        if (severity == Severity.Off)
        {
            yield break;
        }

        yield return new Violation
        {
            RuleId = Id,
            Category = Category,
            Severity = severity,
            MigrationId = migration.Id,
            Source = source,
            Target = target,
            Message = message,
            SafeAlternative = safeAlternative,
        };
    }
}
