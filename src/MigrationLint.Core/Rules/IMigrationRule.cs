using MigrationLint.Core.Engine;
using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

public interface IMigrationRule
{
    string Id { get; }
    string Title { get; }
    Severity DefaultSeverity { get; }
    RuleCategory Category { get; }
    IEnumerable<Violation> Analyze(MigrationOperationIr op, LintContext ctx);
}
