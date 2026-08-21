namespace Umbraco.Automate.Salesforce.Api;

/// <summary>
/// Cleans a free-text, comma-separated field list before it gets spliced into a SOQL SELECT list
/// or a REST <c>?fields=</c> query string. Without this, a trailing/leading comma or stray
/// whitespace (an easy typo in a plain text setting) produces malformed SOQL/query params and the
/// call fails outright — found during the actions/triggers edge-case audit (docs/dev-notes.md §0a).
/// </summary>
internal static class SalesforceFieldListHelper
{
    /// <summary>
    /// Splits <paramref name="raw"/> on commas, trims each entry, and drops empty entries,
    /// rejoining with commas. Returns an empty string for null/whitespace input.
    /// </summary>
    public static string CleanCommaSeparatedList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(",", raw
            .Split(',')
            .Select(f => f.Trim())
            .Where(f => f.Length > 0));
    }
}
