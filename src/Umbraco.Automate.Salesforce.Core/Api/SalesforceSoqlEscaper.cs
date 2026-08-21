namespace Umbraco.Automate.Salesforce.Api;

/// <summary>
/// Escapes a value for safe interpolation into a SOQL string literal (docs/dev-notes.md §7/§8: "never
/// string-concatenate user/binding values into SOQL"). Salesforce's SOQL/SOSL parser treats
/// <c>\</c> as an escape character and <c>'</c> as a string delimiter — escaping both prevents a
/// bound value from breaking out of its literal.
/// </summary>
/// <remarks>
/// This defends the value itself; it does not make a fully free-text, bindable SOQL template
/// injection-proof — Core's <c>${ }</c> binding substitution happens <em>before</em> an action's
/// <c>ExecuteAsync</c> runs, so a raw template string handed to an action has already had bound
/// values spliced in with no chance for the action to intervene. Actions that build a WHERE
/// clause programmatically from separate, non-bindable field/operator settings plus a bindable
/// value (see <c>QueryRecordsAction</c>) can call this <em>before</em> splicing that value in and
/// get a real guarantee; an action that accepts one fully free-text bindable SOQL string cannot.
/// </remarks>
public static class SalesforceSoqlEscaper
{
    /// <summary>
    /// Escapes <c>\</c> and <c>'</c> so <paramref name="value"/> is safe to wrap in single quotes
    /// as a SOQL string literal.
    /// </summary>
    public static string EscapeStringLiteral(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
