using System.Globalization;
using System.Text;

namespace MigrationLint.Core.Formatting;

/// <summary>
/// Minimal JSON emitter. Hand-rolled so <c>MigrationLint.Core</c> keeps zero runtime
/// dependencies on netstandard2.0 (System.Text.Json is not in-box there).
/// </summary>
internal static class Json
{
    public static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    public static string Str(string? value) => value is null ? "null" : $"\"{Escape(value)}\"";
}
