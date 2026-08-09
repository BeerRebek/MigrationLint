using MigrationLint.Core.Model;

namespace MigrationLint.Core.Rules;

/// <summary>
/// Provider annotation keys. These are EF/provider-version sensitive — keep them here,
/// one per constant, with a fixture per key (see CONTRIBUTING: verify per EF major).
/// </summary>
public static class ProviderAnnotations
{
    public const string NpgsqlCreatedConcurrently = "Npgsql:CreatedConcurrently";
    public const string SqlServerOnline = "SqlServer:Online";

    /// <summary>True when the annotation is present and its value reads as truthy.</summary>
    public static bool IsTruthy(MigrationOperationIr op, string key)
    {
        if (!op.Annotations.TryGetValue(key, out var value))
        {
            return false;
        }

        return value is not null &&
               (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
    }
}
