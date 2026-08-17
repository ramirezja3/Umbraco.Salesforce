using System.Text.Json;

namespace Umbraco.Automate.Salesforce.Api;

/// <summary>
/// Small conversions between <see cref="JsonElement"/> and the loosely-typed dictionaries used
/// by action outputs — Salesforce record shape isn't statically typed in v1 (CLAUDE.md §0a: no
/// live Describe-metadata picker yet), so record fields surface as a plain field-name → value map.
/// </summary>
internal static class SalesforceJsonHelpers
{
    /// <summary>
    /// Converts a JSON object into a field-name → value dictionary. Nested objects/arrays are
    /// kept as their raw <see cref="JsonElement"/>; scalars are unwrapped to plain CLR values.
    /// </summary>
    public static Dictionary<string, object?> ToFieldDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in element.EnumerateObject())
        {
            // Salesforce includes an "attributes" object (type/url) on every record — not a field.
            if (property.NameEquals("attributes"))
            {
                continue;
            }

            result[property.Name] = Unwrap(property.Value);
        }

        return result;
    }

    private static object? Unwrap(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone(),
    };
}
