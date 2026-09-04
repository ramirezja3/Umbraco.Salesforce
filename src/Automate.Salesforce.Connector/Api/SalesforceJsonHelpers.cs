using System.Text.Json;

namespace Automate.Salesforce.Connector.Api;

/// <summary>
/// Small conversions between <see cref="JsonElement"/> and the loosely-typed dictionaries used to
/// inspect a Salesforce record's fields in tests — this package has no live object/field picker
/// (docs/dev-notes.md §5), so a returned record's fields surface as a plain field-name → value map
/// rather than a statically-typed model.
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

    /// <summary>
    /// Converts a value produced by <see cref="Unwrap"/> to a <see cref="double"/>, regardless of
    /// whether it was boxed as <see cref="long"/> (whole-number JSON literals, e.g. a round-dollar
    /// Amount like <c>50000</c>) or <see cref="double"/> (anything with a decimal point).
    /// </summary>
    /// <remarks>
    /// A boxed <see cref="long"/> can never satisfy an <c>as double?</c> cast — the C# <c>as</c>
    /// operator does not perform numeric conversions between boxed value types, it only succeeds
    /// when the runtime type already matches. Doing <c>(object)5L as double?</c> silently
    /// evaluates to <c>null</c> instead of throwing, so callers should use this instead of an
    /// <c>as</c> cast for any numeric field pulled out of a field dictionary (e.g. an Opportunity
    /// Amount that happens to have no cents, and therefore parses as a whole-number JSON literal).
    /// </remarks>
    public static double? ToDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        long l => l,
        int i => i,
        decimal m => (double)m,
        float f => f,
        _ => null,
    };
}
