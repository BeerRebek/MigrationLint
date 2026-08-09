using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// Base for all rules. A rule is a pure function of <c>(operation, context)</c> — no I/O,
/// no syntax nodes, no allocation-heavy work. See §5.3 of the PRD.
/// </summary>
public abstract class RuleBase : IMigrationRule
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract Severity DefaultSeverity { get; }
    public abstract RuleCategory Category { get; }

    public abstract IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx);

    protected static readonly IEnumerable<Violation> None = Array.Empty<Violation>();

    /// <summary>Builds a single-item result at the effective severity for this rule.</summary>
    protected IEnumerable<Violation> One(
        MigrationOperationIr op,
        LintContext ctx,
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
            MigrationId = ctx.MigrationId,
            Source = op.Source,
            Target = op.Target,
            Message = message,
            SafeAlternative = safeAlternative,
        };
    }
}
