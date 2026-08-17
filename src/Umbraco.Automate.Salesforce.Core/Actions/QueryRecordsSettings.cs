using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="QueryRecordsAction"/>.
/// </summary>
public sealed class QueryRecordsSettings
{
    /// <summary>
    /// Gets or sets the SOQL query to run, e.g. <c>SELECT Id, Name FROM Account WHERE Industry = 'Technology'</c>.
    /// Values may use <c>${ }</c> bindings — see <see cref="QueryRecordsAction"/>'s remarks on the
    /// injection-safety limits of that, and prefer <c>Umbraco.Automate.Salesforce.Api.SalesforceSoqlEscaper</c>
    /// when splicing a bound value into a string literal yourself.
    /// </summary>
    [Field(Label = "SOQL Query", Description = "The SOQL SELECT statement to run.",
        SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Soql { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of rows to return. Capped by the server-side
    /// <c>Umbraco:Automate:Salesforce:MaxQueryRows</c> setting regardless of what's configured here.
    /// </summary>
    [Field(Label = "Max Rows", Description = "Maximum number of rows to return.", SortOrder = 1)]
    public int MaxRows { get; set; } = 200;
}
